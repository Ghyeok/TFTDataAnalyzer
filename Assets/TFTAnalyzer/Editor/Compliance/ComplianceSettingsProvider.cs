using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.Compliance
{
    public sealed class ComplianceSettingsProvider : SettingsProvider
    {
        public const string SettingsPath = "Project/TFT Data Analyzer/Compliance";
        private readonly ComplianceSettingsStore _store;
        private readonly Func<DateTimeOffset> _clock;
        private ComplianceSettingsReadResult _saved;
        private TextField _reviewDate;
        private DropdownField _registration;
        private Toggle _confirmed;
        private Label _savedState;
        private Label _result;
        private Button _save;

        public ComplianceSettingsProvider(ComplianceSettingsStore store, Func<DateTimeOffset> clock)
            : base(SettingsPath, SettingsScope.Project)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            keywords = new HashSet<string>(new[] { "TFT", "Compliance", "정책", "등록" });
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new ComplianceSettingsProvider(
            new ComplianceSettingsStore(Path.GetDirectoryName(UnityEngine.Application.dataPath)), () => DateTimeOffset.UtcNow);

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            OnDeactivate();
            rootElement.Clear();
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uss");
            if (stylesheet != null && !rootElement.styleSheets.Contains(stylesheet)) rootElement.styleSheets.Add(stylesheet);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "compliance-scroll" };
            scroll.AddToClassList("tft-data-scroll");
            var content = new VisualElement(); content.AddToClassList("tft-data-content");
            scroll.Add(content); rootElement.Add(scroll);
            AddLabel(content, "정책과 등록 확인 기록", "tft-tab-title");
            AddLabel(content, "Riot Portal에서 직접 확인한 상태를 기록합니다. 로컬 기록은 Riot의 온라인 승인 검증이 아닙니다. 정책 링크 열기와 화면 방문은 확인 시각을 만들지 않습니다.");
            _saved = _store.Load();
            _savedState = AddLabel(content, PolicyCopy.StateText(_saved)); _savedState.name = "compliance-saved-state";
            _reviewDate = new TextField("정책 검토일 (yyyy-MM-dd)") { name = "compliance-review-date" };
            _reviewDate.AddToClassList("tft-data-field");
            _reviewDate.SetValueWithoutNotify(_saved.Settings?.policyReviewedOn ?? ""); content.Add(_reviewDate);
            _registration = new DropdownField("사용자가 확인한 등록 상태", new List<string> { "Unverified", "ApprovedOrAcknowledged", "Withdrawn" }, 0)
                { name = "compliance-registration" };
            _registration.AddToClassList("tft-data-field");
            _registration.SetValueWithoutNotify(_saved.Settings?.registrationStatus ?? "Unverified"); content.Add(_registration);
            AddLabel(content, "Unverified: 등록 미확인 · ApprovedOrAcknowledged: Portal에서 확인한 등록 기록 · Withdrawn: 등록 철회");
            _confirmed = new Toggle("Riot Portal에서 이 제품의 등록 상태를 직접 확인했습니다.") { name = "compliance-portal-confirmed" };
            _confirmed.AddToClassList("tft-compliance-confirmation");
            content.Add(_confirmed);
            AddLabel(content, "확인 체크 후 저장할 때만 현재 UTC 시각을 기록합니다. 등록 미확인·철회 상태에는 확인 시각을 저장하지 않습니다.");
            _save = new Button(SaveDraft) { name = "compliance-save", text = "확인 기록 저장" }; content.Add(_save);
            _result = AddLabel(content, "입력은 저장 전 초안입니다."); _result.name = "compliance-save-result";
        }

        private void SaveDraft()
        {
            var now = _clock();
            var approved = _registration.value == "ApprovedOrAcknowledged";
            var priorAcknowledgement = _saved.State == ComplianceReadState.Valid &&
                _saved.Settings.registrationStatus == "ApprovedOrAcknowledged" ? _saved.Settings.acknowledgedAt : null;
            if (approved && !_confirmed.value && priorAcknowledgement == null)
            { _result.text = "Portal 상태를 직접 확인한 뒤 확인 항목을 선택해 주세요."; return; }
            var draft = new ComplianceSettings
            {
                policyReviewedOn = _reviewDate.value,
                policyUrls = PolicyCopy.Urls,
                registrationStatus = _registration.value,
                acknowledgedAt = !approved ? null : _confirmed.value ?
                    now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture) : priorAcknowledgement
            };
            if (!CompliancePolicy.IsWellFormed(draft) ||
                (!string.IsNullOrEmpty(draft.policyReviewedOn) && DateTime.ParseExact(draft.policyReviewedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date > now.UtcDateTime.Date) ||
                (approved && !CompliancePolicy.CanPublish(draft, now)))
            { _result.text = "검토일과 등록 확인 항목을 확인해 주세요. 미래 날짜와 올바르지 않은 값은 저장할 수 없습니다."; return; }
            try { _store.Save(draft); }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            { _result.text = "확인 기록을 저장하지 못했습니다. 기존 저장 기록과 입력 초안은 유지됩니다."; return; }
            _saved = new ComplianceSettingsReadResult(ComplianceReadState.Valid, draft);
            _savedState.text = PolicyCopy.StateText(_saved);
            _confirmed.SetValueWithoutNotify(false);
            _result.text = "확인 기록을 저장했습니다.";
        }

        public override void OnDeactivate()
        {
            if (_save != null) _save.clicked -= SaveDraft;
            _save = null;
        }

        private static Label AddLabel(VisualElement parent, string text, string className = "tft-wrapped-text")
        {
            var label = new Label(text); label.AddToClassList(className); parent.Add(label); return label;
        }
    }
}
