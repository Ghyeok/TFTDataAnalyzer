using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Analysis;

namespace TFTAnalyzer.Editor.UI.Data
{
    public enum CatalogRecordKind { Unit, Trait, Item, Augment }

    public sealed class CatalogBrowserRecord
    {
        public CatalogRecordKind Kind { get; }
        public string StableId { get; }
        public string NameText { get; }
        public string DetailsText { get; }
        public string IconPath { get; }
        internal LocalizedText Name { get; }
        internal CatalogBrowserRecord(CatalogRecordKind kind, string id, LocalizedText name, string icon, string details)
        { Kind = kind; StableId = id; Name = name; NameText = CatalogBrowserViewModel.Display(name); IconPath = icon; DetailsText = details; }
    }

    public sealed class CatalogBrowserViewModel : IDisposable
    {
        public const int PageSize = 50;
        private StaticCatalog _catalog;
        private CatalogBrowserRecord[] _all = Array.Empty<CatalogBrowserRecord>(), _filtered = Array.Empty<CatalogBrowserRecord>();
        private bool _disposed;
        public event Action Changed;
        public CatalogIdentity Identity { get; private set; }
        public CatalogRecordKind Kind { get; private set; }
        public string Search { get; private set; } = "";
        public IReadOnlyList<CatalogBrowserRecord> Rows { get; private set; } = Array.Empty<CatalogBrowserRecord>();
        public int TotalCount => _filtered.Length;
        public int Page { get; private set; }
        public int PageCount => (TotalCount + PageSize - 1) / PageSize;
        public CatalogBrowserRecord Selected { get; private set; }
        public string CatalogText => Identity == null ? "활성 카탈로그 없음" :
            $"{Identity.Patch.SetId} (세트 {Identity.Patch.SetNumber}) · TFT {Identity.Patch.PatchId} · 원본 {Identity.Patch.SourceGameVersion} · {Identity.ProfileId}";
        public string StatusText => Identity == null ? "데이터 관리에서 검증된 카탈로그를 선택해 주세요." :
            TotalCount == 0 ? "검색 결과가 없습니다." : $"{KindName(Kind)} {TotalCount}개 · {Page + 1}/{PageCount}페이지";

        public void SetCatalog(StaticCatalog catalog, CatalogIdentity identity)
        {
            if (_disposed) return;
            if (!AnalysisService.MatchesCatalog(catalog, identity)) { catalog = null; identity = null; }
            if (ReferenceEquals(_catalog, catalog) && (Identity == null ? identity == null : AnalysisService.SameIdentity(Identity, identity))) return;
            _catalog = catalog; Identity = identity;
            _all = catalog == null ? Array.Empty<CatalogBrowserRecord>() : Project(catalog).ToArray();
            Filter();
        }

        public void SetFilter(CatalogRecordKind kind, string search)
        {
            if (_disposed) return;
            if (!Enum.IsDefined(typeof(CatalogRecordKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            search = search ?? "";
            if (search.Length > 256) search = search.Substring(0, 256);
            if (Kind == kind && Search == search) return;
            Kind = kind; Search = search; Filter();
        }

        public void SetPage(int page)
        {
            if (_disposed) return;
            page = Math.Max(0, Math.Min(page, PageCount - 1));
            if (Page == page) return;
            Page = page; Selected = null; Slice(); Changed?.Invoke();
        }

        public void Select(string stableId)
        {
            if (_disposed) return;
            var selected = Rows.FirstOrDefault(row => row.StableId == stableId);
            if (ReferenceEquals(Selected, selected)) return;
            Selected = selected; Changed?.Invoke();
        }

        private void Filter()
        {
            bool Contains(string value) => value.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0;
            _filtered = _all.Where(row => row.Kind == Kind && (Contains(row.StableId) || Contains(row.Name.KoKr) || Contains(row.Name.EnUs)))
                .OrderBy(row => row.StableId, StringComparer.Ordinal).ToArray();
            Page = 0; Selected = null; Slice(); Changed?.Invoke();
        }

        private void Slice() => Rows = Array.AsReadOnly(_filtered.Skip(Page * PageSize).Take(PageSize).ToArray());

        internal static string KindName(CatalogRecordKind kind) => new[] { "기물", "시너지", "아이템", "증강" }[(int)kind];
        internal static string Clip(string value) => string.IsNullOrWhiteSpace(value) ? "자료 없음" :
            value.Length > 4096 ? value.Substring(0, 4096) + "… (일부 내용 생략)" : value;
        internal static string Display(LocalizedText text) => Clip(text?.DisplayValue);
        private static string LanguageNotice(LocalizedText text) => string.IsNullOrWhiteSpace(text.DisplayValue) ? "자료 없음" :
            text.UsedEnglishFallback ? "한국어 없음 — 영어로 대체" : "한국어 우선";

        private static IEnumerable<CatalogBrowserRecord> Project(StaticCatalog catalog)
        {
            var names = catalog.Units.Select(x => (CatalogRecordKind.Unit, x.StableId, x.Name))
                .Concat(catalog.Traits.Select(x => (CatalogRecordKind.Trait, x.StableId, x.Name)))
                .Concat(catalog.Items.Select(x => (CatalogRecordKind.Item, x.StableId, x.Name)))
                .Concat(catalog.Augments.Select(x => (CatalogRecordKind.Augment, x.StableId, x.Name))).ToArray();
            string Related(IEnumerable<string> ids, CatalogRecordKind? target)
            {
                return string.Join("\n", ids.Select(id =>
                {
                    var matches = names.Where(x => x.StableId == id && (!target.HasValue || x.Item1 == target.Value)).ToArray();
                    return matches.Length == 0 ? $"{Clip(id)} — 카탈로그 내 미수록(이름 없음)" :
                        string.Join("\n", matches.Select(x => $"{KindName(x.Item1)}: {Display(x.Name)} [{Clip(id)}]"));
                }));
            }
            foreach (var x in catalog.Units)
                yield return Create(catalog, CatalogRecordKind.Unit, x.StableId, x.Name, x.Description, x.IconPath, x.Sources, x.Effects,
                    ("비용", x.Cost.ToString(CultureInfo.InvariantCulture)), ("역할", x.Role), ("관계", Related(x.TraitIds, CatalogRecordKind.Trait)));
            foreach (var x in catalog.Traits)
                yield return Create(catalog, CatalogRecordKind.Trait, x.StableId, x.Name, x.Description, x.IconPath, x.Sources, x.Effects);
            foreach (var x in catalog.Items)
                yield return Create(catalog, CatalogRecordKind.Item, x.StableId, x.Name, x.Description, x.IconPath, x.Sources, x.Effects,
                    ("관계", Related(x.ComponentIds, CatalogRecordKind.Item)));
            foreach (var x in catalog.Augments)
                yield return Create(catalog, CatalogRecordKind.Augment, x.StableId, x.Name, x.Description, x.IconPath, x.Sources, x.Effects,
                    ("등급", x.Tier), ("관계", Related(x.RelatedIds, null)));
        }

        private static CatalogBrowserRecord Create(StaticCatalog catalog, CatalogRecordKind kind, string id, LocalizedText name, LocalizedText description,
            string icon, IEnumerable<SourceStamp> sources, IReadOnlyDictionary<string, double> effects, params (string Label, string Value)[] extra)
        {
            var fields = new List<(string Label, string Value)>
            {
                ("종류", KindName(kind)), ("안정 ID", id),
                ("필드별 출처", FieldSources(catalog, kind, id)),
                ("이름(한국어)", name.KoKr), ("이름(영어)", name.EnUs),
                ("이름 표시", LanguageNotice(name)),
                ("설명(한국어)", description.KoKr), ("설명(영어)", description.EnUs),
                ("설명 표시", LanguageNotice(description))
            };
            fields.AddRange(extra);
            fields.Add(("효과", string.Join("\n", effects.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => Clip(x.Key) + ": " + x.Value.ToString("R", CultureInfo.InvariantCulture)))));
            foreach (var source in sources)
            {
                fields.Add(("출처", source.SourceName)); fields.Add(("원본 URL", source.Url));
                fields.Add(("출처 조회 UTC", source.RetrievedUtc <= DateTime.UnixEpoch ?
                    "정규화 기본값 — 실제 수집 시각은 상단 로컬 카탈로그 참고" : source.RetrievedUtc.ToString("O", CultureInfo.InvariantCulture)));
                fields.Add(("ETag", source.ETag)); fields.Add(("원본 SHA-256", source.Sha256));
                fields.Add(("보완 출처", source.IsSupplemental ? "예" : "아니요"));
            }
            if (!sources.Any()) fields.Add(("출처", ""));
            return new CatalogBrowserRecord(kind, id, name, icon, string.Join("\n\n", fields.Select(field => field.Label + ": " + Clip(field.Value))));
        }

        private static string FieldSources(StaticCatalog catalog, CatalogRecordKind kind, string id)
        {
            if (catalog.Manifest.SchemaVersion < 3) return "필드별 출처 정보 없음 — 기존 문서 단위 출처만 보존된 카탈로그입니다.";
            var type = new[] { "unit", "trait", "item", "augment" }[(int)kind];
            var origins = catalog.FieldOrigins.Where(x => x.RecordType == type && x.StableId == id)
                .OrderBy(x => x.Pointer, StringComparer.Ordinal).ToArray();
            if (origins.Length == 0) return "필드별 출처 기록 없음 — 검증이 필요합니다.";
            return string.Join("\n\n", origins.Select(x => Clip(x.Pointer) +
                (x.Pointer == "/iconPath" ? " (로컬 변환 전 원본 아이콘 참조의 출처)" : "") + "\n" +
                string.Join("\n", x.Sources.Select(source =>
                    (source.IsSupplemental ? "보완" : "공식") + " · " + Clip(source.SourceName) + "\n" +
                    Clip(source.Url) + "\nSHA-256: " + Clip(source.Sha256)))));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Changed = null; _catalog = null; Identity = null; Selected = null;
            _all = _filtered = Array.Empty<CatalogBrowserRecord>(); Rows = Array.Empty<CatalogBrowserRecord>();
        }
    }
}
