using System;
using System.IO;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Persistence
{
    public sealed class StaticCatalogPaths
    {
        public const string GeneratedAssetDirectory = "Assets/TFTAnalyzer/Data/Generated";
        public const string CatalogIndexAssetPath = GeneratedAssetDirectory + "/CatalogIndex.asset";

        public string ProjectRoot { get; }
        public string GeneratedRelativeDirectory { get; }
        public string CacheRelativeDirectory { get; }
        public string IndexAssetPath => GeneratedRelativeDirectory + "/CatalogIndex.asset";

        public StaticCatalogPaths(string projectRoot, string generatedRelativeDirectory, string cacheRelativeDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || projectRoot.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                projectRoot.StartsWith(@"\\.\", StringComparison.Ordinal))
                throw new ArgumentException("Project root is invalid.", nameof(projectRoot));
            ProjectRoot = Path.GetFullPath(projectRoot);
            GeneratedRelativeDirectory = ValidateRelative(generatedRelativeDirectory, "Assets");
            CacheRelativeDirectory = ValidateRelative(cacheRelativeDirectory, "Library");
            SafePath(GeneratedRelativeDirectory);
            SafePath(CacheRelativeDirectory);
        }
        public string StagingDirectory => SafePath(CacheRelativeDirectory + "/staging");
        public string RawRootDirectory => SafePath(CacheRelativeDirectory + "/raw");
        public string IconsDirectory => SafePath(GeneratedRelativeDirectory + "/Icons");

        public StaticCatalogPaths(string projectRoot)
            : this(projectRoot, GeneratedAssetDirectory, "Library/TftAnalyzerCache") { }

        public string CanonicalFinalDirectory(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return AbsoluteAssetPath(CatalogAssetPath(identity).Substring(0,
                CatalogAssetPath(identity).Length - "/catalog.json".Length));
        }

        public string LegacyFinalDirectory(PatchKey patch)
        {
            ValidatePatch(patch);
            return SafePath($"{GeneratedRelativeDirectory}/{patch.SetId}/{patch.PatchId}");
        }

        public string RawDirectory(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return SafePath($"{CacheRelativeDirectory}/raw/{identity.Patch.SetId}/{identity.Patch.PatchId}/{identity.Patch.SourceGameVersion}/{identity.ProfileId}");
        }

        public string CatalogAssetPath(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return $"{GeneratedRelativeDirectory}/{identity.Patch.SetId}/{identity.Patch.PatchId}/{identity.Patch.SourceGameVersion}/{identity.ProfileId}/catalog.json";
        }

        public string LegacyCatalogAssetPath(PatchKey patch)
        {
            ValidatePatch(patch);
            return $"{GeneratedRelativeDirectory}/{patch.SetId}/{patch.PatchId}/catalog.json";
        }

        public string CreateStagingDirectory()
        {
            var path = Path.Combine(StagingDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public string AbsoluteAssetPath(string assetPath)
        {
            var relative = ValidateRelative(assetPath, "Assets");
            if (!string.Equals(relative, GeneratedRelativeDirectory, StringComparison.Ordinal) &&
                !relative.StartsWith(GeneratedRelativeDirectory + "/", StringComparison.Ordinal))
                throw new ArgumentException("Asset path is outside the generated root.");
            return SafePath(relative);
        }

        internal void EnsureStagingPath(string path) => EnsureWithin(path, StagingDirectory);
        internal void EnsureGeneratedPath(string path) => EnsureWithin(path, SafePath(GeneratedRelativeDirectory));

        private static void EnsureWithin(string path, string root)
        {
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Catalog path is outside its owned root.");
            AnalyzerStorePaths.RejectReparsePoints(full);
        }

        private string SafePath(string relative)
        {
            var path = Path.GetFullPath(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(ProjectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Catalog path escapes the project.");
            AnalyzerStorePaths.RejectReparsePoints(path);
            return path;
        }

        private static string ValidateRelative(string path, string area)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) throw new ArgumentException("Relative project path is required.");
            var normalized = path.Replace('\\', '/');
            var segments = normalized.Split('/');
            if (segments.Length < 2 || !string.Equals(segments[0], area, StringComparison.Ordinal))
                throw new ArgumentException("Project path is outside its allowed area.");
            foreach (var segment in segments) ValidateSegment(segment, nameof(path));
            return normalized;
        }

        private static void ValidatePatch(PatchKey patch)
        {
            if (patch == null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            ValidateSegment(patch.SetId, nameof(patch.SetId));
            ValidateSegment(patch.PatchId, nameof(patch.PatchId));
            ValidateSegment(patch.SourceGameVersion, nameof(patch.SourceGameVersion));
        }

        private static void ValidateIdentity(CatalogIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            ValidatePatch(identity.Patch);
            ValidateSegment(identity.ProfileId, nameof(identity.ProfileId));
        }

        private static void ValidateSegment(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Contains("..") ||
                value.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.EndsWith(".", StringComparison.Ordinal) ||
                value.EndsWith(" ", StringComparison.Ordinal))
            {
                throw new ArgumentException("Path segment is unsafe.", name);
            }
        }
    }
}
