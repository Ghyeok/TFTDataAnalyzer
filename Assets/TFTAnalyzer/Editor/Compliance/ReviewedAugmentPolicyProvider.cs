using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Editor.Compliance
{
    /// <summary>Describes local availability, without claiming policy approval or online verification.</summary>
    public enum AugmentPolicyLoadState { Missing, Loaded, Invalid }
    /// <summary>Publishes metadata only after the entire approved document has been validated.</summary>
    public sealed class AugmentPolicyLoadResult
    {
        public AugmentPolicyLoadState State { get; }
        public MetricPolicyMetadata Metadata { get; }
        private AugmentPolicyLoadResult(AugmentPolicyLoadState state, MetricPolicyMetadata metadata = null) { State = state; Metadata = metadata; }
        public static AugmentPolicyLoadResult Missing { get; } = new AugmentPolicyLoadResult(AugmentPolicyLoadState.Missing);
        public static AugmentPolicyLoadResult Invalid { get; } = new AugmentPolicyLoadResult(AugmentPolicyLoadState.Invalid);
        public static AugmentPolicyLoadResult Loaded(MetricPolicyMetadata metadata) => new AugmentPolicyLoadResult(AugmentPolicyLoadState.Loaded, metadata ?? throw new ArgumentNullException(nameof(metadata)));
    }
    public interface IAugmentPolicyMetadataProvider
    {
        AugmentPolicyLoadResult Load(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token);
    }
    /// <summary>A code-reviewed registration; a document cannot add itself to this trust boundary.</summary>
    internal sealed class ReviewedAugmentPolicyRegistration
    {
        internal CatalogIdentity Catalog { get; }
        internal string ContentHash { get; }
        internal string DocumentHash { get; }
        internal DateTime ReviewedUtc { get; }
        internal IReadOnlyList<string> Sources { get; }
        internal ReviewedAugmentPolicyRegistration(CatalogIdentity catalog, string contentHash, string documentHash, DateTime reviewedUtc, IEnumerable<string> sources)
        {
            var copy = sources?.ToArray();
            if (catalog == null || !IsHash(contentHash) || !IsHash(documentHash) || reviewedUtc.Kind != DateTimeKind.Utc ||
                reviewedUtc == DateTime.MinValue || copy == null || copy.Length == 0 ||
                copy.Any(source => !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("augment_policy_registration_invalid");
            Catalog = catalog; ContentHash = contentHash; DocumentHash = documentHash;
            ReviewedUtc = reviewedUtc; Sources = Array.AsReadOnly(copy);
        }
        internal static bool IsHash(string value) => value != null && Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z");
    }
    /// <summary>Reads only hash-pinned, code-approved local policy packs during an explicit analysis refresh.</summary>
    public sealed class ReviewedAugmentPolicyProvider : IAugmentPolicyMetadataProvider
    {
        // No real classification evidence has been reviewed yet. Adding entries requires a separate data review.
        private static readonly ReviewedAugmentPolicyRegistration[] Approved = Array.Empty<ReviewedAugmentPolicyRegistration>();
        private readonly string _root;
        private readonly ReviewedAugmentPolicyRegistration[] _registrations;
        private readonly Func<string, Stream> _openRead;
        public ReviewedAugmentPolicyProvider(string projectRoot) : this(projectRoot, Approved) { }
        internal ReviewedAugmentPolicyProvider(string projectRoot, IEnumerable<ReviewedAugmentPolicyRegistration> registrations, Func<string, Stream> openRead = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathFullyQualified(projectRoot) || projectRoot.StartsWith(@"\\", StringComparison.Ordinal))
                throw new ArgumentException("augment_policy_root_invalid");
            _root = Path.GetFullPath(projectRoot);
            _registrations = registrations?.ToArray() ?? throw new ArgumentNullException(nameof(registrations));
            if (_registrations.Any(x => x == null) || _registrations.Where((x, i) => _registrations.Take(i).Any(previous =>
                AnalysisService.SameIdentity(previous.Catalog, x.Catalog) && previous.ContentHash == x.ContentHash)).Any())
                throw new ArgumentException("augment_policy_registration_invalid");
            _openRead = openRead ?? (path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        /// <summary>Fails closed without exposing paths, parser details or untrusted document values.</summary>
        public AugmentPolicyLoadResult Load(StaticCatalog catalog, CatalogIdentity identity, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!AnalysisService.MatchesCatalog(catalog, identity)) return AugmentPolicyLoadResult.Invalid;
            var registration = _registrations.SingleOrDefault(x => AnalysisService.SameIdentity(x.Catalog, identity) && x.ContentHash == catalog.Manifest.ContentHash);
            if (registration == null) return AugmentPolicyLoadResult.Missing;
            var path = Path.Combine(_root, "Assets", "TFTAnalyzer", "Data", "Policy", registration.DocumentHash + ".json");
            try
            {
                AnalyzerStorePaths.RejectReparsePoints(path);
                byte[] bytes;
                using (var stream = _openRead(path))
                using (var buffer = new MemoryStream())
                {
                    AnalyzerStorePaths.RejectReparsePoints(path);
                    var chunk = new byte[4096]; int read;
                    while ((read = stream.Read(chunk, 0, Math.Min(chunk.Length, AugmentPolicyDocumentReader.MaximumBytes + 1 - (int)buffer.Length))) != 0)
                    {
                        token.ThrowIfCancellationRequested();
                        buffer.Write(chunk, 0, read);
                        if (buffer.Length > AugmentPolicyDocumentReader.MaximumBytes) return AugmentPolicyLoadResult.Invalid;
                    }
                    bytes = buffer.ToArray();
                }
                token.ThrowIfCancellationRequested();
                if (ContentHasher.Sha256Hex(bytes) != registration.DocumentHash) return AugmentPolicyLoadResult.Invalid;
                var metadata = AugmentPolicyDocumentReader.Read(bytes, catalog, identity, token);
                token.ThrowIfCancellationRequested();
                return AugmentPolicyLoadResult.Loaded(metadata);
            }
            catch (FileNotFoundException) { return AugmentPolicyLoadResult.Missing; }
            catch (DirectoryNotFoundException) { return AugmentPolicyLoadResult.Missing; }
            catch (InvalidDataException) { return AugmentPolicyLoadResult.Invalid; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is SecurityException ||
                error is JsonException || error is ArgumentException || error is OverflowException)
            { return AugmentPolicyLoadResult.Invalid; }
        }
    }
}
