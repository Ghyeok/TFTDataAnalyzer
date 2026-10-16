using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>Publishes one rebuilt aggregate scope atomically without modifying source observations or other scopes.</summary>
    public sealed class AggregateStore
    {
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
