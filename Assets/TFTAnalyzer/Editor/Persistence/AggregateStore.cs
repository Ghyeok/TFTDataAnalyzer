using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>Signals that analysis cache cleanup is needed without exposing local paths.</summary>
    public sealed class AggregateGenerationLimitException : IOException
    {
        public bool IsStagingLimit { get; }
        public AggregateGenerationLimitException(bool isStagingLimit = false) : base("aggregate_generation_limit")
        { IsStagingLimit = isStagingLimit; }
    }

    /// <summary>Publishes one rebuilt aggregate scope atomically without modifying source observations or other scopes.</summary>
    public sealed class AggregateStore
    {
        public const int MaximumGenerations = 32;
        private readonly AnalyzerStorePaths _paths;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary>Uses the same approved root and cross-instance operation lease as observation persistence.</summary>
        public AggregateStore(AnalyzerStorePaths paths) { _paths = paths ?? throw new ArgumentNullException(nameof(paths)); }

        /// <summary>Flushes canonical counts to staging and replaces only this scope, preserving the prior file on precommit failure.</summary>
        public async Task SaveAsync(ScopeAggregate aggregate, CancellationToken token)
        {
            if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));
            await _gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var bytes = new UTF8Encoding(false, true).GetBytes(Serialize(aggregate));
                _paths.EnsureSafe();
                Directory.CreateDirectory(_paths.RootDirectory);
                _paths.EnsureSafe();
                using (new FileStream(_paths.LeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var target = _paths.AggregatePath(aggregate.Scope, AggregateSnapshot.CurrentSchemaVersion);
                    AnalyzerStorePaths.RejectReparsePoints(target);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    AnalyzerStorePaths.RejectReparsePoints(target);
                    var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            await file.WriteAsync(bytes, 0, bytes.Length, token);
                            token.ThrowIfCancellationRequested();
                            file.Flush(true);
                        }
                        token.ThrowIfCancellationRequested();
                        if (File.Exists(target)) File.Replace(temporary, target, null);
                        else File.Move(temporary, target);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw new IOException("anonymous_aggregate_store_failed"); }
            finally { _gate.Release(); }
        }

        /// <summary>Atomically publishes a complete, unique generation and returns its directory after commit.</summary>
        public async Task<string> SaveGenerationAsync(IEnumerable<ScopeAggregate> aggregates, CancellationToken token)
        {
            if (aggregates == null) throw new ArgumentNullException(nameof(aggregates));
            await _gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                _paths.EnsureSafe();
                Directory.CreateDirectory(_paths.RootDirectory);
                _paths.EnsureSafe();
                using (new FileStream(_paths.LeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var root = _paths.AggregateGenerationsDirectory;
                    AnalyzerStorePaths.RejectReparsePoints(root);
                    Directory.CreateDirectory(root);
                    AnalyzerStorePaths.RejectReparsePoints(root);
                    if (Directory.GetDirectories(root, ".staging-*").Length >= MaximumGenerations)
                        throw new AggregateGenerationLimitException(true);
                    var id = Guid.NewGuid().ToString("N");
                    var staging = Path.Combine(root, ".staging-" + id);
                    var destination = Path.Combine(root, "generation-" + id);
                    AnalyzerStorePaths.RejectReparsePoints(staging);
                    AnalyzerStorePaths.RejectReparsePoints(destination);
                    if (Directory.Exists(staging) || File.Exists(staging) || Directory.Exists(destination) || File.Exists(destination))
                        throw new IOException("aggregate_generation_collision");
                    Directory.CreateDirectory(staging);
                    var ownsStaging = true;
                    var created = new List<string>();
                    var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
                    try
                    {
                        AnalyzerStorePaths.RejectReparsePoints(staging);
                        foreach (var aggregate in aggregates)
                        {
                            token.ThrowIfCancellationRequested();
                            if (aggregate == null) throw new InvalidDataException("aggregate_scope_missing");
                            var path = Path.Combine(staging, Path.GetFileName(_paths.AggregatePath(aggregate.Scope)));
                            AnalyzerStorePaths.RejectReparsePoints(path);
                            var bytes = new UTF8Encoding(false, true).GetBytes(Serialize(aggregate));
                            hashes.Add(path, ContentHasher.Sha256Hex(bytes));
                            token.ThrowIfCancellationRequested();
                            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            {
                                created.Add(path);
                                await file.WriteAsync(bytes, 0, bytes.Length, token);
                                token.ThrowIfCancellationRequested();
                                file.Flush(true);
                            }
                        }
                        token.ThrowIfCancellationRequested();
                        VerifyStaging(staging, created, hashes, token);
                        AnalyzerStorePaths.RejectReparsePoints(destination);
                        var generations = Directory.GetDirectories(root, "generation-*")
                            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                        foreach (var previous in generations)
                        {
                            token.ThrowIfCancellationRequested();
                            if (SameGeneration(staging, previous, token))
                            {
                                VerifyStaging(staging, created, hashes, token);
                                return previous;
                            }
                        }
                        if (generations.Length >= MaximumGenerations)
                            throw new AggregateGenerationLimitException();
                        VerifyStaging(staging, created, hashes, token);
                        token.ThrowIfCancellationRequested();
                        Directory.Move(staging, destination);
                        ownsStaging = false;
                        return destination;
                    }
                    finally
                    {
                        if (ownsStaging && Directory.Exists(staging))
                        {
                            AnalyzerStorePaths.RejectReparsePoints(staging);
                            foreach (var path in created)
                            {
                                AnalyzerStorePaths.RejectReparsePoints(path);
                                if (File.Exists(path)) File.Delete(path);
                            }
                            Directory.Delete(staging, false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (AggregateGenerationLimitException) { throw; }
            catch (Exception) { throw new IOException("anonymous_aggregate_store_failed"); }
            finally { _gate.Release(); }
        }

        /// <summary>Reuses only complete generations whose exact file names and bytes match the staged output.</summary>
        private static bool SameGeneration(string staging, string previous, CancellationToken token)
        {
            var source = Directory.GetFiles(staging).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            string[] target;
            try
            {
                AnalyzerStorePaths.RejectReparsePoints(previous);
                if (Directory.GetDirectories(previous).Length != 0) return false;
                target = Directory.GetFiles(previous).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            if (source.Length != target.Length) return false;
            for (var i = 0; i < source.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!string.Equals(Path.GetFileName(source[i]), Path.GetFileName(target[i]), StringComparison.Ordinal)) return false;
                AnalyzerStorePaths.RejectReparsePoints(source[i]);
                using (var a = new FileStream(source[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    FileStream candidate;
                    try
                    {
                        AnalyzerStorePaths.RejectReparsePoints(target[i]);
                        candidate = new FileStream(target[i], FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (IOException) { return false; }
                    catch (UnauthorizedAccessException) { return false; }
                    using (var b = candidate)
                    {
                        long targetLength;
                        try { targetLength = b.Length; }
                        catch (IOException) { return false; }
                        if (a.Length != targetLength) return false;
                        var left = new byte[65536]; var right = new byte[65536];
                        int length;
                        while ((length = a.Read(left, 0, left.Length)) != 0)
                        {
                            token.ThrowIfCancellationRequested();
                            var count = 0;
                            while (count < length)
                            {
                                int read;
                                try { read = b.Read(right, count, length - count); }
                                catch (IOException) { return false; }
                                if (read == 0) return false;
                                count += read;
                            }
                            for (var n = 0; n < length; n++) if (left[n] != right[n]) return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>Rejects missing, unexpected or changed files in the owned staging directory.</summary>
        private static void VerifyStaging(string staging, IReadOnlyList<string> created,
            IReadOnlyDictionary<string, string> hashes, CancellationToken token)
        {
            AnalyzerStorePaths.RejectReparsePoints(staging);
            if (Directory.GetDirectories(staging).Length != 0 || Directory.GetFiles(staging).Length != created.Count)
                throw new IOException("aggregate_generation_staging_changed");
            foreach (var path in created)
            {
                token.ThrowIfCancellationRequested();
                AnalyzerStorePaths.RejectReparsePoints(path);
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var hash = System.Security.Cryptography.SHA256.Create())
                {
                    var buffer = new byte[65536]; int count;
                    while ((count = file.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        token.ThrowIfCancellationRequested();
                        hash.TransformBlock(buffer, 0, count, buffer, 0);
                    }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var actual = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(actual, hashes[path], StringComparison.Ordinal))
                        throw new IOException("aggregate_generation_staging_changed");
                }
            }
        }

        /// <summary>Encodes versioned raw numerators, denominators, exclusions and observed periods with fixed field order.</summary>
        private static string Serialize(ScopeAggregate value)
        {
            var scope = value.Scope;
            var patch = scope.ActiveCatalog.Patch;
            return new JObject
            {
                ["schemaVersion"] = AggregateSnapshot.CurrentSchemaVersion,
                ["scope"] = new JObject
                {
                    ["setId"] = patch.SetId, ["setNumber"] = patch.SetNumber, ["patchId"] = patch.PatchId,
                    ["sourceGameVersion"] = patch.SourceGameVersion, ["sourceProfile"] = scope.ActiveCatalog.ProfileId,
                    ["region"] = scope.Region, ["queueId"] = scope.QueueId, ["tierBucket"] = scope.TierBucket
                },
                ["matchCount"] = value.MatchCount,
                ["firstPlayedAt"] = Utc(value.FirstPlayedAt), ["lastPlayedAt"] = Utc(value.LastPlayedAt),
                ["matchDataVersions"] = new JArray(value.MatchDataVersions),
                ["matchGameVersions"] = new JArray(value.MatchGameVersions),
                ["metrics"] = new JArray(value.Metrics.Select(row => new JObject
                {
                    ["fingerprint"] = row.Fingerprint.Value, ["count"] = row.Count,
                    ["augmentIds"] = new JArray(row.AugmentIds),
                    ["placementSum"] = row.PlacementSum, ["topFourCount"] = row.TopFourCount,
                    ["firstPlaceCount"] = row.FirstPlaceCount,
                    ["firstPlayedAt"] = Utc(row.FirstPlayedAt), ["lastPlayedAt"] = Utc(row.LastPlayedAt)
                })),
                ["pairs"] = new JArray(value.Pairs.Select(row => new JObject
                {
                    ["first"] = row.First.Value, ["second"] = row.Second.Value,
                    ["firstAugmentIds"] = new JArray(row.FirstAugmentIds), ["secondAugmentIds"] = new JArray(row.SecondAugmentIds),
                    ["count"] = row.Count, ["firstHigherCount"] = row.FirstHigherCount,
                    ["secondHigherCount"] = row.SecondHigherCount, ["ambiguousLobbyCount"] = row.AmbiguousLobbyCount,
                    ["firstPlayedAt"] = Utc(row.FirstPlayedAt), ["lastPlayedAt"] = Utc(row.LastPlayedAt)
                }))
            }.ToString(Formatting.None) + "\n";
        }

        /// <summary>Formats observed match timestamps without culture or local-time dependence.</summary>
        private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
