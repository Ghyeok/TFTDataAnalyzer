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
        public string StagingDirectory => Path.Combine(ProjectRoot, "Library", "TftAnalyzerCache", "staging");
        public string RawRootDirectory => Path.Combine(ProjectRoot, "Library", "TftAnalyzerCache", "raw");
        public string IconsDirectory => Path.Combine(
            ProjectRoot,
            "Assets",
            "TFTAnalyzer",
            "Data",
            "Generated",
            "Icons");

        public StaticCatalogPaths(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root cannot be blank.", nameof(projectRoot));
            }

            ProjectRoot = Path.GetFullPath(projectRoot);
        }

        public string CanonicalFinalDirectory(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return Path.Combine(
                ProjectRoot,
                "Assets",
                "TFTAnalyzer",
                "Data",
                "Generated",
                identity.Patch.SetId,
                identity.Patch.PatchId,
                identity.Patch.SourceGameVersion,
                identity.ProfileId);
        }

        public string LegacyFinalDirectory(PatchKey patch)
        {
            ValidatePatch(patch);
            return Path.Combine(ProjectRoot, "Assets", "TFTAnalyzer", "Data", "Generated",
                patch.SetId, patch.PatchId);
        }

        public string RawDirectory(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return Path.Combine(RawRootDirectory, identity.Patch.SetId, identity.Patch.PatchId,
                identity.Patch.SourceGameVersion, identity.ProfileId);
        }

        public string CatalogAssetPath(CatalogIdentity identity)
        {
            ValidateIdentity(identity);
            return $"{GeneratedAssetDirectory}/{identity.Patch.SetId}/{identity.Patch.PatchId}/{identity.Patch.SourceGameVersion}/{identity.ProfileId}/catalog.json";
        }

        public string LegacyCatalogAssetPath(PatchKey patch)
        {
            ValidatePatch(patch);
            return $"{GeneratedAssetDirectory}/{patch.SetId}/{patch.PatchId}/catalog.json";
        }

        public string CreateStagingDirectory()
        {
            var path = Path.Combine(StagingDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public string AbsoluteAssetPath(string assetPath)
        {
            return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
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
