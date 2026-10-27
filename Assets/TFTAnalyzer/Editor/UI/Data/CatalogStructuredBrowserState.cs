using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed partial class CatalogBrowserViewModel
    {
        public const int StructuredPageSize = 20;
        public const int ExtensionTextPageSize = 4096;
        private CatalogStructuredRow[] _structuredAll = Array.Empty<CatalogStructuredRow>(), _extensionsAll = Array.Empty<CatalogStructuredRow>();
        private string[] _extensionChunks = Array.Empty<string>();
        private int _selectedExtensionIndex = -1;
        public IReadOnlyList<CatalogStructuredRow> StructuredRows { get; private set; } = Array.Empty<CatalogStructuredRow>();
        public IReadOnlyList<CatalogStructuredRow> ExtensionRows { get; private set; } = Array.Empty<CatalogStructuredRow>();
        public int StructuredPage { get; private set; }
        public int ExtensionFieldsPage { get; private set; }
        public int ExtensionPage { get; private set; }
        public int StructuredPageCount => (_structuredAll.Length + StructuredPageSize - 1) / StructuredPageSize;
        public int ExtensionFieldsPageCount => (_extensionsAll.Length + StructuredPageSize - 1) / StructuredPageSize;
        public int ExtensionPageCount => _extensionChunks.Length;
        public CatalogStructuredRow SelectedExtension => _selectedExtensionIndex < 0 ? null : _extensionsAll[_selectedExtensionIndex];
        public string ExtensionText => _extensionChunks.Length == 0 ? "" : _extensionChunks[ExtensionPage];
        public string StructuredNotice => Selected == null ? "항목을 선택하면 구조화 자료를 확인할 수 있습니다." :
            _catalog.Manifest.SchemaVersion < 4 ? "기존 카탈로그에는 구조화 자료가 저장되지 않았습니다. 기존 파일은 그대로 보존됩니다." :
            "원본 수치와 슬롯을 그대로 표시합니다. 슬롯의 성급 의미와 장착 제한은 별도 근거가 필요합니다.";
        public string StructuredStatus => _structuredAll.Length == 0 ? "구조화 자료 없음" : $"{_structuredAll.Length}개 · {StructuredPage + 1}/{StructuredPageCount}페이지";
        public string ExtensionFieldsStatus => _extensionsAll.Length == 0 ? "원본 확장 자료 없음" : $"{_extensionsAll.Length}개 · {ExtensionFieldsPage + 1}/{ExtensionFieldsPageCount}페이지";
        public string ExtensionTextStatus => SelectedExtension == null ? "원문을 볼 항목을 선택해 주세요." :
            $"{SelectedExtension.Label} · {ExtensionPage + 1}/{ExtensionPageCount}페이지";

        private void ResetStructured()
        {
            _structuredAll = Array.Empty<CatalogStructuredRow>(); _extensionsAll = Array.Empty<CatalogStructuredRow>();
            StructuredRows = Array.Empty<CatalogStructuredRow>(); ExtensionRows = Array.Empty<CatalogStructuredRow>();
            StructuredPage = 0; ExtensionFieldsPage = 0; ResetExtensionText();
        }
        private void ResetExtensionText() { _selectedExtensionIndex = -1; _extensionChunks = Array.Empty<string>(); ExtensionPage = 0; }
        private void BuildStructured()
        {
            ResetStructured(); if (Selected == null) return;
            var type = new[] { "unit", "trait", "item", "augment" }[(int)Selected.Kind];
            var all = CatalogStructuredDetails.Project(_catalog, type, Selected.StableId);
            _structuredAll = all.Where(x => x.ExtensionJson == null).ToArray(); _extensionsAll = all.Where(x => x.ExtensionJson != null).ToArray(); SliceStructured();
        }
        private void SliceStructured()
        {
            StructuredRows = Array.AsReadOnly(_structuredAll.Skip(StructuredPage * StructuredPageSize).Take(StructuredPageSize).ToArray());
            ExtensionRows = Array.AsReadOnly(_extensionsAll.Skip(ExtensionFieldsPage * StructuredPageSize).Take(StructuredPageSize).ToArray());
        }
        public void SetStructuredPage(int page)
        {
            if (_disposed) return; page = Math.Max(0, Math.Min(page, StructuredPageCount - 1)); if (page == StructuredPage) return;
            StructuredPage = page; SliceStructured(); Changed?.Invoke();
        }
        public void SetExtensionFieldsPage(int page)
        {
            if (_disposed) return; page = Math.Max(0, Math.Min(page, ExtensionFieldsPageCount - 1)); if (page == ExtensionFieldsPage) return;
            ExtensionFieldsPage = page; ResetExtensionText(); SliceStructured(); Changed?.Invoke();
        }
        public void SelectStructuredRow(int index)
        {
            if (_disposed) return; if (index < 0 || index >= _extensionsAll.Length) index = -1;
            if (_selectedExtensionIndex == index) return; ResetExtensionText(); _selectedExtensionIndex = index;
            if (index >= 0)
            {
                var raw = _extensionsAll[index].ExtensionJson; var chunks = new List<string>();
                for (var start = 0; start < raw.Length;)
                {
                    var length = Math.Min(ExtensionTextPageSize, raw.Length - start);
                    if (start + length < raw.Length && char.IsHighSurrogate(raw[start + length - 1]) && char.IsLowSurrogate(raw[start + length])) length--;
                    chunks.Add(raw.Substring(start, length)); start += length;
                }
                _extensionChunks = chunks.ToArray();
            }
            Changed?.Invoke();
        }
        public void SetExtensionPage(int page)
        {
            if (_disposed) return; page = Math.Max(0, Math.Min(page, ExtensionPageCount - 1)); if (page == ExtensionPage) return;
            ExtensionPage = page; Changed?.Invoke();
        }
    }
}
