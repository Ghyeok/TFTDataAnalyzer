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
            _vm.Changed += QueueRender;
            Render();
        }

        private void FilterChanged(ChangeEvent<string> evt)
        { if (!_disposed && !_rendering) _vm.SetFilter((CatalogRecordKind)Math.Max(0, _kind.index), _search.value); }
        private void SelectionChanged(IEnumerable<object> rows)
        { if (!_disposed && !_rendering) _vm.Select(rows.OfType<CatalogBrowserRecord>().FirstOrDefault()?.StableId); }
        private void Previous() { if (!_disposed) _vm.SetPage(_vm.Page - 1); }
        private void Next() { if (!_disposed) _vm.SetPage(_vm.Page + 1); }
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
            _icon.image = null; _iconRecord = null;
        }
    }
}
