using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TFTAnalyzer.Editor.Persistence;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Data
{
    public sealed class CatalogBrowserTab : IDisposable
    {
        private readonly VisualElement _root;
        private readonly CatalogBrowserViewModel _vm;
        private readonly Func<string, Texture2D> _loadIcon;
        private readonly DropdownField _kind;
        private readonly TextField _search;
        private readonly ListView _list;
        private readonly Button _previous, _next;
        private readonly Button _structuredPrevious, _structuredNext, _extensionPrevious, _extensionNext, _rawPrevious, _rawNext;
        private readonly VisualElement _structuredRows, _extensionRows;
        private readonly TextField _rawText;
        private readonly Image _icon;
        private CatalogBrowserRecord _iconRecord;
        private bool _disposed, _pending, _rendering;

        public CatalogBrowserTab(VisualElement root, CatalogBrowserViewModel model, Func<string, Texture2D> loadIcon)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _vm = model ?? throw new ArgumentNullException(nameof(model));
            _loadIcon = loadIcon ?? throw new ArgumentNullException(nameof(loadIcon));
            _kind = root.Q<DropdownField>("browser-kind"); _search = root.Q<TextField>("browser-search");
            _list = root.Q<ListView>("browser-results"); _icon = root.Q<Image>("browser-icon");
            _previous = root.Q<Button>("browser-previous"); _next = root.Q<Button>("browser-next");
            _structuredPrevious = root.Q<Button>("browser-structured-previous"); _structuredNext = root.Q<Button>("browser-structured-next");
            _extensionPrevious = root.Q<Button>("browser-extension-previous"); _extensionNext = root.Q<Button>("browser-extension-next");
            _rawPrevious = root.Q<Button>("browser-extension-text-previous"); _rawNext = root.Q<Button>("browser-extension-text-next");
            _structuredRows = root.Q("browser-structured-rows"); _extensionRows = root.Q("browser-extension-rows");
            _rawText = root.Q<TextField>("browser-extension-text"); _rawText.isReadOnly = true;
            _kind.choices = Enum.GetValues(typeof(CatalogRecordKind)).Cast<CatalogRecordKind>().Select(CatalogBrowserViewModel.KindName).ToList();
            _search.maxLength = 256;
            _list.fixedItemHeight = 26; _list.selectionType = SelectionType.Single;
            _list.makeItem = () => { var label = new Label { enableRichText = false }; label.AddToClassList("tft-browser-result"); return label; };
            _list.bindItem = (element, index) =>
            {
                var row = (CatalogBrowserRecord)_list.itemsSource[index];
                ((Label)element).text = row.NameText + " [" + CatalogBrowserViewModel.Clip(row.StableId) + "]";
            };
            foreach (var label in root.Query<Label>().ToList()) label.enableRichText = false;
            _kind.RegisterValueChangedCallback(FilterChanged); _search.RegisterValueChangedCallback(FilterChanged);
            _list.selectionChanged += SelectionChanged;
            _previous.clicked += Previous; _next.clicked += Next;
            _structuredPrevious.clicked += StructuredPrevious; _structuredNext.clicked += StructuredNext;
            _extensionPrevious.clicked += ExtensionPrevious; _extensionNext.clicked += ExtensionNext;
            _rawPrevious.clicked += RawPrevious; _rawNext.clicked += RawNext;
            _vm.Changed += QueueRender;
            Render();
        }

        private void FilterChanged(ChangeEvent<string> evt)
        { if (!_disposed && !_rendering) _vm.SetFilter((CatalogRecordKind)Math.Max(0, _kind.index), _search.value); }
        private void SelectionChanged(IEnumerable<object> rows)
        { if (!_disposed && !_rendering) _vm.Select(rows.OfType<CatalogBrowserRecord>().FirstOrDefault()?.StableId); }
        private void Previous() { if (!_disposed) _vm.SetPage(_vm.Page - 1); }
        private void Next() { if (!_disposed) _vm.SetPage(_vm.Page + 1); }
        private void StructuredPrevious() { if (!_disposed) _vm.SetStructuredPage(_vm.StructuredPage - 1); }
        private void StructuredNext() { if (!_disposed) _vm.SetStructuredPage(_vm.StructuredPage + 1); }
        private void ExtensionPrevious() { if (!_disposed) _vm.SetExtensionFieldsPage(_vm.ExtensionFieldsPage - 1); }
        private void ExtensionNext() { if (!_disposed) _vm.SetExtensionFieldsPage(_vm.ExtensionFieldsPage + 1); }
        private void RawPrevious() { if (!_disposed) _vm.SetExtensionPage(_vm.ExtensionPage - 1); }
        private void RawNext() { if (!_disposed) _vm.SetExtensionPage(_vm.ExtensionPage + 1); }
        private void QueueRender()
        {
            if (_disposed || _pending) return;
            _pending = true; EditorApplication.update += RenderPending;
        }
        private void RenderPending()
        {
            EditorApplication.update -= RenderPending; _pending = false;
            if (!_disposed) Render();
        }

        private void Render()
        {
            _rendering = true;
            try
            {
                _kind.SetValueWithoutNotify(CatalogBrowserViewModel.KindName(_vm.Kind));
                _search.SetValueWithoutNotify(_vm.Search);
                _root.Q<Label>("browser-catalog").text = CatalogBrowserViewModel.Clip(_vm.CatalogText);
                _root.Q<Label>("browser-status").text = _vm.StatusText;
                _list.itemsSource = _vm.Rows.ToList(); _list.Rebuild();
                var index = _vm.Selected == null ? -1 : _vm.Rows.ToList().IndexOf(_vm.Selected);
                _list.SetSelectionWithoutNotify(index < 0 ? Array.Empty<int>() : new[] { index });
                _previous.SetEnabled(_vm.Page > 0); _next.SetEnabled(_vm.Page + 1 < _vm.PageCount);
                _root.Q("browser-details").EnableInClassList("tft-tab-hidden", _vm.Selected == null);
                _root.Q("browser-selection-notice").EnableInClassList("tft-tab-hidden", _vm.Selected != null);
                _root.Q<Label>("browser-detail-text").text = _vm.Selected?.DetailsText ?? "";
                RenderStructured();
                if (!ReferenceEquals(_iconRecord, _vm.Selected))
                {
                    _iconRecord = _vm.Selected; _icon.image = null;
                    try { if (_iconRecord != null && AllowedIcon(_iconRecord.IconPath)) _icon.image = _loadIcon(_iconRecord.IconPath); }
                    catch (Exception) { _icon.image = null; }
                }
                _icon.EnableInClassList("tft-tab-hidden", _icon.image == null);
                _root.Q("browser-icon-missing").EnableInClassList("tft-tab-hidden", _icon.image != null);
            }
            finally { _rendering = false; }
        }

        private static bool AllowedIcon(string path) => path != null && Regex.IsMatch(path,
            "\\AAssets/TFTAnalyzer/Data/Generated/Icons/[0-9a-f]{64}\\.png\\z");

        private void RenderStructured()
        {
            _root.Q<Label>("browser-structured-notice").text = _vm.StructuredNotice;
            _root.Q<Label>("browser-structured-status").text = _vm.StructuredStatus;
            _root.Q<Label>("browser-extension-status").text = _vm.ExtensionFieldsStatus;
            _root.Q<Label>("browser-extension-text-status").text = _vm.ExtensionTextStatus;
            _structuredPrevious.SetEnabled(_vm.StructuredPage > 0); _structuredNext.SetEnabled(_vm.StructuredPage + 1 < _vm.StructuredPageCount);
            _extensionPrevious.SetEnabled(_vm.ExtensionFieldsPage > 0); _extensionNext.SetEnabled(_vm.ExtensionFieldsPage + 1 < _vm.ExtensionFieldsPageCount);
            _rawPrevious.SetEnabled(_vm.ExtensionPage > 0); _rawNext.SetEnabled(_vm.ExtensionPage + 1 < _vm.ExtensionPageCount);
            _rawText.SetValueWithoutNotify(_vm.ExtensionText);
            _structuredRows.Clear(); _extensionRows.Clear();
            VisualElement Row(CatalogStructuredRow value)
            {
                var row = new VisualElement(); row.AddToClassList("tft-structured-row");
                var field = new Label(value.Label + " [" + value.Field + "]") { enableRichText = false }; field.AddToClassList("tft-wrapped-text"); row.Add(field);
                var text = new Label(value.Value) { enableRichText = false }; text.AddToClassList("tft-structured-value"); row.Add(text);
                var source = new Label(value.SourceText) { enableRichText = false }; source.AddToClassList("tft-wrapped-text"); row.Add(source);
                return row;
            }
            foreach (var value in _vm.StructuredRows) _structuredRows.Add(Row(value));
            for (var i = 0; i < _vm.ExtensionRows.Count; i++)
            {
                var index = _vm.ExtensionFieldsPage * CatalogBrowserViewModel.StructuredPageSize + i;
                var row = Row(_vm.ExtensionRows[i]); var open = new Button(() => { if (!_disposed) _vm.SelectStructuredRow(index); }) { text = "원문 보기" };
                row.Add(open); _extensionRows.Add(row);
            }
        }

        internal static Texture2D LoadLocalIcon(string projectRoot, string assetPath)
        {
            if (!AllowedIcon(assetPath) || string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathFullyQualified(projectRoot) ||
                projectRoot.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            var full = Path.Combine(projectRoot, assetPath);
            AnalyzerStorePaths.RejectReparsePoints(full);
            if (!File.Exists(full)) return null;
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            AnalyzerStorePaths.RejectReparsePoints(full);
            return image;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; EditorApplication.update -= RenderPending; _pending = false;
            _vm.Changed -= QueueRender;
            _kind.UnregisterValueChangedCallback(FilterChanged); _search.UnregisterValueChangedCallback(FilterChanged);
            _list.selectionChanged -= SelectionChanged; _list.itemsSource = null; _list.bindItem = null; _list.makeItem = null;
            _previous.clicked -= Previous; _next.clicked -= Next;
            _structuredPrevious.clicked -= StructuredPrevious; _structuredNext.clicked -= StructuredNext;
            _extensionPrevious.clicked -= ExtensionPrevious; _extensionNext.clicked -= ExtensionNext;
            _rawPrevious.clicked -= RawPrevious; _rawNext.clicked -= RawNext;
            _structuredRows.Clear(); _extensionRows.Clear(); _rawText.SetValueWithoutNotify("");
            _icon.image = null; _iconRecord = null;
        }
    }
}
