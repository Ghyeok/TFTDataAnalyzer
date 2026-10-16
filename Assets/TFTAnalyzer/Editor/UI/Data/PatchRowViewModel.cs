using System;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed class PatchRowViewModel
    {
        public CatalogIndexEntry Entry { get; }
        public CatalogIdentity Identity { get; }
        public string Directory { get; }
        public StaticCatalog Catalog { get; }
        public DateTime RetrievedUtc { get; }
        public int ValidationErrorCount { get; }
        public int ValidationWarningCount { get; }
        public string ErrorCode { get; }
        public bool IsSelectable => Identity != null && Catalog != null &&
                                    ValidationErrorCount == 0 && string.IsNullOrEmpty(ErrorCode);
        public string ContentHashPrefix => (Entry.ContentHash ?? "").Substring(0,
            Math.Min(12, (Entry.ContentHash ?? "").Length));
        public string SourceBadge => Identity == null ? "출처 확인 필요" :
            Identity.IncludesCommunityDragon ? "Riot + CommunityDragon" : "Riot 공식";
        public string DisplayIdentity => $"{Entry.SetId} (세트 {Entry.SetNumber}) · TFT {Entry.PatchId} · " +
                                         $"원본 {Entry.SourceGameVersion} · {Identity?.ProfileId ?? Entry.SourceProfileId}";
        public string ValidationText => !IsSelectable ? "검증 실패 · 선택 불가" :
            ValidationWarningCount > 0 ? $"검증 통과 · 경고 {ValidationWarningCount}개" : "검증 통과";

        public PatchRowViewModel(CatalogIndexEntry entry, CatalogIdentity identity, string directory,
            StaticCatalog catalog, DateTime retrievedUtc, int validationErrorCount,
            int validationWarningCount, string errorCode)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Identity = identity;
            Directory = directory ?? "";
            Catalog = catalog;
            RetrievedUtc = retrievedUtc;
            ValidationErrorCount = validationErrorCount;
            ValidationWarningCount = validationWarningCount;
            ErrorCode = errorCode ?? "";
        }
    }
}
