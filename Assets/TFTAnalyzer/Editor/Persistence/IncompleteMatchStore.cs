using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Persistence
{
    internal enum IncompleteSaveResult { Saved, Duplicate, Full }

    internal sealed class IncompleteStoredRecord
    {
        internal string Handle { get; }
        internal IncompleteMatchCandidate Candidate { get; }
        internal bool ReferenceAvailable { get; }
        internal StaticCatalog ReferenceCatalog { get; }
        internal bool IsCorrupt => Candidate == null;
        internal IncompleteStoredRecord(string handle, IncompleteMatchCandidate candidate, StaticCatalog referenceCatalog)
        { Handle = handle; Candidate = candidate; ReferenceCatalog = referenceCatalog; ReferenceAvailable = referenceCatalog != null; }
    }

    /// <summary>Separate local store; it never writes response bytes or a raw match identifier.</summary>
    internal sealed class IncompleteMatchStore
    {
        private const int MaxRecords = 128;
        private const int MaxBytes = 256 * 1024;
        private readonly AnalyzerStorePaths paths;
        private readonly Action<string, string> publish;
        private readonly Action staged;
        private string DirectoryPath => Path.Combine(paths.RootDirectory, "incomplete-matches");
        private string LeasePath => Path.Combine(DirectoryPath, "store.lock");

        internal IncompleteMatchStore(AnalyzerStorePaths paths, Action staged = null, Action<string, string> publish = null)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.staged = staged;
            this.publish = publish ?? File.Move;
        }

        internal IncompleteSaveResult Save(MatchReadWithIncompleteResult parsed, CatalogIndex index, CatalogIdentity identity, CancellationToken token)
        {
            if (parsed?.IncompleteCandidate == null || parsed.IncompleteMatchHash == null ||
                !Regex.IsMatch(parsed.IncompleteMatchHash, "\\A[0-9a-f]{64}\\z")) throw new ArgumentException("incomplete_record_invalid");
            string staging = null;
            try
            {
                token.ThrowIfCancellationRequested();
                var digest = parsed.IncompleteMatchHash;
                var candidate = parsed.IncompleteCandidate;
                var catalog = ResolveCatalog(index, identity, true);
                var catalogHash = CatalogHash(catalog);
                if (identity == null)
                {
                    if (parsed.ReferenceCatalogIdentity != null || parsed.ReferenceCatalogHash != null) throw new InvalidDataException();
                }
                else if (parsed.ReferenceCatalogIdentity == null || !SameIdentity(identity, parsed.ReferenceCatalogIdentity) ||
                         !string.Equals(catalogHash, parsed.ReferenceCatalogHash, StringComparison.Ordinal)) throw new InvalidDataException();
                var data = Encode(candidate);
                // Rebuild through the factory to reject a foreign catalog or forged graph before publication.
                var verified = Decode(data, catalog);
                if (!JToken.DeepEquals(data, Encode(verified))) throw new InvalidDataException();
                var document = new JObject { ["schema"] = 1, ["matchHash"] = digest,
                    ["catalogHash"] = catalogHash == null ? JValue.CreateNull() : new JValue(catalogHash), ["candidate"] = data };
                var bytes = new UTF8Encoding(false, true).GetBytes(document.ToString(Formatting.None));
                if (bytes.Length > MaxBytes) throw new InvalidDataException();
                using (Lease())
                {
                    var all = Load(catalog, token);
                    if (all.Any(x => x.Hash == digest)) return IncompleteSaveResult.Duplicate;
                    if (all.Count >= MaxRecords) return IncompleteSaveResult.Full;
                    var target = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".json");
                    staging = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
                    AnalyzerStorePaths.RejectReparsePoints(staging);
                    using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                    staged?.Invoke();
                    AnalyzerStorePaths.RejectReparsePoints(target);
                    AnalyzerStorePaths.RejectReparsePoints(staging);
                    token.ThrowIfCancellationRequested();
                    publish(staging, target);
                    staging = null;
                    return IncompleteSaveResult.Saved;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw Error(); }
            finally
            {
                if (staging != null) try { AnalyzerStorePaths.RejectReparsePoints(staging); if (File.Exists(staging)) File.Delete(staging); } catch { }
            }
        }

        internal IReadOnlyList<IncompleteStoredRecord> ReadAll(CatalogIndex index, CatalogIdentity identity, CancellationToken token)
        {
            try { token.ThrowIfCancellationRequested(); var catalog = ResolveCatalog(index, identity, false);
                using (Lease()) return Load(catalog, token, true).Select(x => new IncompleteStoredRecord(x.Handle, x.Candidate, x.ReferenceCatalog)).ToArray(); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw Error(); }
        }

        internal bool Delete(string handle, CancellationToken token)
        {
            if (handle == null || !Regex.IsMatch(handle, "\\A[0-9a-f]{32}\\z")) throw new ArgumentException("incomplete_handle_invalid");
            try
            {
                token.ThrowIfCancellationRequested();
                using (Lease())
                {
                    var path = Path.Combine(DirectoryPath, handle + ".json");
                    AnalyzerStorePaths.RejectReparsePoints(path);
                    token.ThrowIfCancellationRequested();
                    if (!File.Exists(path)) return false;
                    File.Delete(path);
                    return true;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw Error(); }
        }

        private FileStream Lease()
        {
            paths.EnsureSafe();
            AnalyzerStorePaths.RejectReparsePoints(DirectoryPath);
            Directory.CreateDirectory(DirectoryPath);
            AnalyzerStorePaths.RejectReparsePoints(LeasePath);
            return new FileStream(LeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private sealed class Loaded
        {
            internal string Hash; internal string Handle; internal IncompleteMatchCandidate Candidate; internal StaticCatalog ReferenceCatalog;
        }

        private List<Loaded> Load(StaticCatalog catalog, CancellationToken token, bool includeCorrupt = false)
        {
            var files = Directory.GetFiles(DirectoryPath, "*.json");
            if (files.Length > MaxRecords) throw new InvalidDataException();
            var result = new List<Loaded>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var providedHash = CatalogHash(catalog);
            foreach (var path in files.OrderBy(x => x, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                AnalyzerStorePaths.RejectReparsePoints(path);
                var handle = Path.GetFileNameWithoutExtension(path);
                if (!Regex.IsMatch(handle, "\\A[0-9a-f]{32}\\z")) throw new InvalidDataException();
                try
                {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (file.Length > MaxBytes) throw new InvalidDataException();
                    using (var text = new StreamReader(file, new UTF8Encoding(false, true), false))
                    {
                        var source = text.ReadToEnd();
                        using (var stringReader = new StringReader(source))
                        using (var reader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                        {
                        var doc = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        if (reader.Read() || !Fields(doc, "schema", "matchHash", "catalogHash", "candidate") ||
                            Integer(doc["schema"]) != 1 || doc["matchHash"].Type != JTokenType.String ||
                            (doc["catalogHash"].Type != JTokenType.Null && doc["catalogHash"].Type != JTokenType.String) ||
                            !string.Equals(source, doc.ToString(Formatting.None), StringComparison.Ordinal)) throw new InvalidDataException();
                        var hash = (string)doc["matchHash"];
                        if (hash == null || !Regex.IsMatch(hash, "\\A[0-9a-f]{64}\\z") || !hashes.Add(hash)) throw new InvalidDataException();
                        var refHash = (string)doc["catalogHash"];
                        if (refHash != null && !Regex.IsMatch(refHash, "\\A[0-9a-f]{64}\\z")) throw new InvalidDataException();
                        var selected = refHash != null && refHash == providedHash ? catalog : null;
                        var candidate = Decode((JObject)doc["candidate"], selected);
                        var expected = doc["candidate"].DeepClone();
                        if (selected == null) RedactIds(expected);
                        if (!JToken.DeepEquals(expected, Encode(candidate))) throw new InvalidDataException();
                        result.Add(new Loaded { Handle = handle, Hash = hash, Candidate = candidate, ReferenceCatalog = selected });
                        }
                    }
                }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch
                {
                    if (!includeCorrupt) throw;
                    result.Add(new Loaded { Handle = handle });
                }
            }
            return result;
        }

        private static string CatalogHash(StaticCatalog catalog) => catalog == null ? null :
            ContentHasher.Sha256Hex(CanonicalCatalogSerializer.Serialize(catalog));
        private static void RedactIds(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name == "id") RedactId(property.Value);
                    else if ((property.Name == "items" || property.Name == "augments") && property.Value is JArray list && list.Count == 2 && list[1] is JArray values)
                        foreach (var value in values) RedactId(value);
                    else RedactIds(property.Value);
                }
            }
            else if (token is JArray array) foreach (var child in array) RedactIds(child);
        }
        private static void RedactId(JToken token)
        {
            if (token is JArray id && id.Count == 2 && id[0].Type == JTokenType.Integer && (int)id[0] == (int)CandidateFieldState.Value)
            { id[0] = (int)CandidateFieldState.CatalogUnconfirmed; id[1] = JValue.CreateNull(); }
        }
        private StaticCatalog ResolveCatalog(CatalogIndex index, CatalogIdentity identity, bool required)
        {
            if (identity == null) return null;
            var row = new PublishedCatalogLoader(new StaticCatalogPaths(paths.ProjectRoot)).Load(index)
                .SingleOrDefault(x => x.IsSelectable && SameIdentity(x.Identity, identity));
            if (row == null && required) throw new InvalidDataException();
            return row?.Catalog;
        }
        private static bool SameIdentity(CatalogIdentity left, CatalogIdentity right) =>
            left != null && right != null && left.Equals(right) && left.Patch.SetNumber == right.Patch.SetNumber;
        private static bool Fields(JObject value, params string[] names) => value != null &&
            value.Properties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(names.OrderBy(x => x, StringComparer.Ordinal));
        private static IOException Error() => new IOException("incomplete_store_failed");

        private static JObject Encode(IncompleteMatchCandidate match) => new JObject
        {
            ["meaning"] = (int)match.Meaning,
            ["set"] = Number(match.SetNumber), ["queue"] = Number(match.QueueId),
            ["participants"] = List(match.Participants, p => new JObject
            {
                ["ordinal"] = p.LocalOrdinal, ["placement"] = Number(p.Placement), ["round"] = Number(p.LastRound),
                ["units"] = List(p.Units, u => new JObject
                { ["id"] = Id(u.StableId), ["tier"] = Number(u.Tier), ["items"] = List(u.Items, Id) }),
                ["traits"] = List(p.Traits, t => new JObject { ["id"] = Id(t.StableId), ["tier"] = Number(t.Tier) }),
                ["augments"] = List(p.Augments, Id)
            })
        };

        private static JArray Number(CandidateNumberField value) => new JArray((int)value.State,
            value.Value.HasValue ? (JToken)new JValue(value.Value.Value) : JValue.CreateNull());
        private static JArray Id(CandidateIdField value) => new JArray((int)value.State,
            value.Value == null ? JValue.CreateNull() : (JToken)new JValue(value.Value));
        private static JArray List<T>(CandidateList<T> list, Func<T, JToken> encode) =>
            new JArray((int)list.State, new JArray(list.Values.Select(encode)));

        private static IncompleteMatchCandidate Decode(JObject root, StaticCatalog catalog)
        {
            if (!Fields(root, "meaning", "set", "queue", "participants")) throw new InvalidDataException();
            var meaning = Integer(root["meaning"]);
            if (meaning != (int)CandidateMeaning.Confirmed && meaning != (int)CandidateMeaning.Unverified) throw new InvalidDataException();
            var queue = Pair(root["queue"]);
            var requestedQueue = State(queue) == CandidateFieldState.Value ? Integer(queue[1]) : 1100;
            if (requestedQueue <= 0) throw new InvalidDataException();
            var factory = new IncompleteCandidateFactory(catalog, meaning == (int)CandidateMeaning.Confirmed
                ? IncompleteCandidateContext.SupportedVersionPatchConfirmed : IncompleteCandidateContext.PatchUnconfirmed, requestedQueue);
            CandidateNumberField N(JToken token, CandidateNumberKind kind)
            {
                var pair = Pair(token); var state = State(pair);
                if (state == CandidateFieldState.Value)
                {
                    var result = factory.Number(kind, Integer(pair[1]));
                    if (result.State != state) throw new InvalidDataException();
                    return result;
                }
                Null(pair[1]);
                return factory.NumberState(kind, state);
            }
            CandidateIdField I(JToken token, CandidateIdKind kind)
            {
                var pair = Pair(token); var state = State(pair);
                if (state == CandidateFieldState.Value)
                {
                    if (pair[1].Type != JTokenType.String) throw new InvalidDataException();
                    var id = (string)pair[1];
                    if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException();
                    var result = factory.Id(kind, id);
                    if (catalog != null && result.State != CandidateFieldState.Value) throw new InvalidDataException();
                    return result;
                }
                Null(pair[1]);
                return factory.IdState(kind, state);
            }
            T L<T, V>(JToken token, Func<CandidateFieldState, T> missing, Func<IEnumerable<V>, T> make, Func<JToken, V> read, int max)
            {
                var pair = Pair(token); var state = State(pair);
                if (!(pair[1] is JArray values) || values.Count > max) throw new InvalidDataException();
                if (state == CandidateFieldState.Value)
                {
                    if (values.Count == 0) throw new InvalidDataException();
                    return make(values.Select(read).ToArray());
                }
                if (values.Count != 0) throw new InvalidDataException();
                return missing(state);
            }
            IncompleteUnitCandidate U(JToken token)
            {
                var obj = token as JObject;
                if (!Fields(obj, "id", "tier", "items")) throw new InvalidDataException();
                return factory.Unit(I(obj["id"], CandidateIdKind.Unit), N(obj["tier"], CandidateNumberKind.UnitTier),
                    L(obj["items"], factory.ItemsState, factory.Items, x => I(x, CandidateIdKind.Item), 16));
            }
            IncompleteTraitCandidate T(JToken token)
            {
                var obj = token as JObject;
                if (!Fields(obj, "id", "tier")) throw new InvalidDataException();
                return factory.Trait(I(obj["id"], CandidateIdKind.Trait), N(obj["tier"], CandidateNumberKind.TraitTier));
            }
            IncompleteParticipantCandidate P(JToken token)
            {
                var obj = token as JObject;
                if (!Fields(obj, "ordinal", "placement", "round", "units", "traits", "augments")) throw new InvalidDataException();
                return factory.Participant(Integer(obj["ordinal"]), N(obj["placement"], CandidateNumberKind.Placement),
                    N(obj["round"], CandidateNumberKind.LastRound), L(obj["units"], factory.UnitsState, factory.Units, U, 64),
                    L(obj["traits"], factory.TraitsState, factory.Traits, T, 64),
                    L(obj["augments"], factory.AugmentsState, factory.Augments, x => I(x, CandidateIdKind.Augment), 16));
            }
            return factory.Match(N(root["set"], CandidateNumberKind.SetNumber), N(root["queue"], CandidateNumberKind.QueueId),
                L(root["participants"], factory.ParticipantsState, factory.Participants, P, 8));
        }

        private static JArray Pair(JToken token)
        {
            if (!(token is JArray pair) || pair.Count != 2) throw new InvalidDataException();
            return pair;
        }
        private static CandidateFieldState State(JArray pair)
        {
            var state = (CandidateFieldState)Integer(pair[0]);
            if (!Enum.IsDefined(typeof(CandidateFieldState), state)) throw new InvalidDataException();
            return state;
        }
        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new InvalidDataException();
            return checked((int)(long)token);
        }
        private static void Null(JToken token)
        {
            if (token == null || token.Type != JTokenType.Null) throw new InvalidDataException();
        }
    }
}
