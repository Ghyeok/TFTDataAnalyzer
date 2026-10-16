using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>Persists accepted anonymous observations as canonical NDJSON with recoverable hash deduplication.</summary>
    public sealed class ObservationStore
    {
        private const int MaximumRecordBytes = 8 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly AnalyzerStorePaths _paths;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary>Accepts paths only; no raw payload, API key or player identifier enters this store.</summary>
        public ObservationStore(AnalyzerStorePaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        /// <summary>Commits one new match. A post-commit index error leaves its observation recoverable on retry.</summary>
        public async Task<bool> AppendIfNewAsync(AnonymousMatchObservation observation, CancellationToken token)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            await _gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var bytes = Utf8.GetBytes(ObservationJson.Serialize(observation) + "\n");
                if (bytes.Length > MaximumRecordBytes) throw new InvalidDataException();
                using (AcquireLease())
                {
                    var index = new ProcessedMatchIndex();
                    Recover(index, token);
                    if (index.Contains(observation.MatchHash))
                    {
                        token.ThrowIfCancellationRequested();
                        index.Save(_paths);
                        return false;
                    }
                    var path = _paths.ObservationPath(observation.Scope);
                    AnalyzerStorePaths.RejectReparsePoints(path);
                    token.ThrowIfCancellationRequested();
                    var existed = File.Exists(path);
                    try
                    {
                        using (var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        {
                            var originalLength = file.Length;
                            file.Position = originalLength;
                            try
                            {
                                await file.WriteAsync(bytes, 0, bytes.Length, token);
                                token.ThrowIfCancellationRequested();
                                file.Flush(true);
                            }
                            catch
                            {
                                file.SetLength(originalLength);
                                file.Flush(true);
                                throw;
                            }
                        }
                    }
                    catch
                    {
                        if (!existed && File.Exists(path) && new FileInfo(path).Length == 0) File.Delete(path);
                        throw;
                    }
                    // Once durable, cancellation cannot turn a successful commit into an apparent non-write.
                    index.Add(observation.MatchHash);
                    index.Save(_paths);
                    return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw StoreError(); }
            finally { _gate.Release(); }
        }

        /// <summary>Returns a validated snapshot without holding locks while the consumer enumerates it.</summary>
        public async IAsyncEnumerable<AnonymousMatchObservation> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken token)
        {
            var snapshot = await ReadSnapshotAsync(token);
            foreach (var observation in snapshot)
            {
                token.ThrowIfCancellationRequested();
                yield return observation;
            }
        }

        private async Task<List<AnonymousMatchObservation>> ReadSnapshotAsync(CancellationToken token)
        {
            await _gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                using (AcquireLease())
                {
                    var index = new ProcessedMatchIndex();
                    var records = Recover(index, token);
                    token.ThrowIfCancellationRequested();
                    index.Save(_paths);
                    return records;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw StoreError(); }
            finally { _gate.Release(); }
        }

        private FileStream AcquireLease()
        {
            _paths.EnsureSafe();
            Directory.CreateDirectory(_paths.ObservationsDirectory);
            _paths.EnsureSafe();
            // A second instance/process fails safely while this lease is active instead of racing its index.
            return new FileStream(_paths.LeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private List<AnonymousMatchObservation> Recover(ProcessedMatchIndex index, CancellationToken token)
        {
            var records = new List<AnonymousMatchObservation>();
            foreach (var path in Directory.GetFiles(_paths.ObservationsDirectory, "*.ndjson")
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                AnalyzerStorePaths.RejectReparsePoints(path);
                using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                using (var line = new MemoryStream())
                {
                    long completeLength = 0;
                    int next;
                    while ((next = file.ReadByte()) >= 0)
                    {
                        if ((file.Position & 4095) == 0) token.ThrowIfCancellationRequested();
                        if (next != '\n')
                        {
                            if (line.Length >= MaximumRecordBytes) throw new InvalidDataException();
                            line.WriteByte((byte)next);
                            continue;
                        }
                        token.ThrowIfCancellationRequested();
                        var observation = ObservationJson.Deserialize(Utf8.GetString(line.ToArray()));
                        if (!string.Equals(path, _paths.ObservationPath(observation.Scope), StringComparison.OrdinalIgnoreCase) ||
                            !index.Add(observation.MatchHash)) throw new InvalidDataException();
                        records.Add(observation);
                        completeLength = file.Position;
                        line.SetLength(0);
                        line.Position = 0;
                    }
                    token.ThrowIfCancellationRequested();
                    if (file.Length != completeLength)
                    {
                        // Only the unterminated crash tail is discarded; malformed complete records fail above.
                        file.SetLength(completeLength);
                        file.Flush(true);
                    }
                }
            }
            return records;
        }

        private static IOException StoreError() => new IOException("anonymous_observation_store_failed");
    }

    /// <summary>Versioned allowlist encoding; unknown properties or altered canonical records fail closed.</summary>
    internal static class ObservationJson
    {
        internal static string Serialize(AnonymousMatchObservation value)
        {
            var scope = value.Scope;
            var patch = scope.ActiveCatalog.Patch;
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["observation"] = new JObject
                {
                    ["MatchHash"] = value.MatchHash,
                    ["Scope"] = new JObject
                    {
                        ["ActiveCatalog"] = new JObject
                        {
                            ["Patch"] = new JObject
                            {
                                ["SetId"] = patch.SetId, ["SetNumber"] = patch.SetNumber,
                                ["PatchId"] = patch.PatchId, ["SourceGameVersion"] = patch.SourceGameVersion
                            },
                            ["SourceProfile"] = (int)scope.ActiveCatalog.SourceProfile
                        },
                        ["Region"] = scope.Region, ["QueueId"] = scope.QueueId, ["TierBucket"] = scope.TierBucket
                    },
                    ["PlayedAt"] = value.PlayedAt.ToString("O", CultureInfo.InvariantCulture),
                    ["Participants"] = new JArray(value.Participants.Select(participant => new JObject
                    {
                        ["Placement"] = participant.Placement, ["LastRound"] = participant.LastRound,
                        ["Units"] = new JArray(participant.Units.Select(unit => new JObject
                        {
                            ["StableId"] = unit.StableId, ["Tier"] = unit.Tier, ["ItemIds"] = new JArray(unit.ItemIds)
                        })),
                        ["Traits"] = new JArray(participant.Traits.Select(trait => new JObject
                        {
                            ["StableId"] = trait.StableId, ["TierCurrent"] = trait.TierCurrent
                        })),
                        ["AugmentIds"] = new JArray(participant.AugmentIds)
                    })),
                    ["MatchDataVersion"] = value.MatchDataVersion, ["MatchGameVersion"] = value.MatchGameVersion
                }
            }.ToString(Formatting.None);
        }

        internal static AnonymousMatchObservation Deserialize(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
            {
                var envelope = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if ((int?)envelope["schemaVersion"] != 1) throw new InvalidDataException();
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    Culture = CultureInfo.InvariantCulture, TypeNameHandling = TypeNameHandling.None,
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Error
                });
                var observation = envelope["observation"]?.ToObject<AnonymousMatchObservation>(serializer);
                if (observation == null || !string.Equals(Serialize(observation), json, StringComparison.Ordinal))
                    throw new InvalidDataException();
                return observation;
            }
        }
    }
}
