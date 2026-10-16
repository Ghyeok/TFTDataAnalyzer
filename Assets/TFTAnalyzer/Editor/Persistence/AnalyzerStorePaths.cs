using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>Keeps anonymous data outside imported assets and derives filenames from complete scopes.</summary>
    public sealed class AnalyzerStorePaths
    {
        public string ProjectRoot { get; }
        public string RootDirectory { get; }
        public string ObservationsDirectory => Path.Combine(RootDirectory, "observations");
        public string ProcessedMatchIndexPath => Path.Combine(RootDirectory, "processed-match-hashes.json");
        internal string LeasePath => Path.Combine(RootDirectory, "store.lock");

        /// <summary>Allows the default Library store or an explicit project-external directory.</summary>
        public AnalyzerStorePaths(string projectRoot, string rootOverride = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.");
            ProjectRoot = Normalize(projectRoot);
            var defaultRoot = Path.Combine(ProjectRoot, "Library", "TftAnalyzerStore");
            RootDirectory = rootOverride == null ? defaultRoot : Normalize(rootOverride);
            if (IsWithin(RootDirectory, ProjectRoot) &&
                !string.Equals(RootDirectory, defaultRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Observation store must use the default Library directory or an external directory.");
            EnsureSafe();
        }

        /// <summary>Uses a versioned structured encoding of every catalog and filter dimension.</summary>
        public string ObservationPath(MetricScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            var patch = scope.ActiveCatalog.Patch;
            var key = new JArray(patch.SetId, patch.SetNumber, patch.PatchId, patch.SourceGameVersion,
                scope.ActiveCatalog.ProfileId, scope.Region, scope.QueueId, scope.TierBucket);
            return Path.Combine(ObservationsDirectory,
                ContentHasher.Sha256Hex("scope-v1|" + key.ToString(Formatting.None)) + ".ndjson");
        }

        /// <summary>Locates a versioned aggregate using the exact same complete-scope digest as observations.</summary>
        public string AggregatePath(MetricScope scope, int schemaVersion = AggregateSnapshot.CurrentSchemaVersion)
        {
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            return Path.Combine(RootDirectory, "aggregates", schemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Path.GetFileNameWithoutExtension(ObservationPath(scope)) + ".json");
        }

        /// <summary>Rejects existing junctions or symlinks again at each filesystem boundary.</summary>
        internal void EnsureSafe()
        {
            RejectReparsePoints(RootDirectory);
            RejectReparsePoints(ObservationsDirectory);
            RejectReparsePoints(ProcessedMatchIndexPath);
            RejectReparsePoints(LeasePath);
            RejectReparsePoints(Path.Combine(RootDirectory, "aggregates"));
        }

        /// <summary>Rejects a link at the target or any existing ancestor before store access.</summary>
        internal static void RejectReparsePoints(string path)
        {
            for (var current = Normalize(path); current != null; current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Observation store links are unsupported.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        /// <summary>Resolves a nonblank absolute path while preserving filesystem roots.</summary>
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Store path is required.");
            var full = Path.GetFullPath(path);
            return full.Length == Path.GetPathRoot(full).Length ? full :
                full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>Tests directory containment using a separator boundary and case-insensitive comparison.</summary>
        private static bool IsWithin(string path, string parent) =>
            string.Equals(path, parent, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
