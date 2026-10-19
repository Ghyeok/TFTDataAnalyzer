using System;
using System.Collections.Generic;
using TFTAnalyzer.Editor.Compliance;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.About
{
    public sealed class AboutTab : IDisposable
    {
        private readonly AboutViewModel _model;
        private readonly Label _state;
        private readonly List<KeyValuePair<Button, Action>> _actions = new List<KeyValuePair<Button, Action>>();
        private bool _disposed;

        public AboutTab(VisualElement root, AboutViewModel model, Action openSettings, Action<string> openUrl)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            root.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "about-scroll" };
            scroll.AddToClassList("tft-data-scroll");
            var content = new VisualElement(); content.AddToClassList("tft-data-content");
            scroll.Add(content); root.Add(scroll);
            AddLabel(content, "데이터 출처와 정책", true);
            AddLabel(content, "정적 데이터: Riot Data Dragon. CommunityDragon 보충 자료는 가져오기마다 명시적으로 선택한 경우에만 사용합니다. 과거 경기: Riot Match API.");
            AddLabel(content, "등록과 정책 확인 기록", true);
            _state = AddLabel(content, ""); _state.name = "about-compliance-state";
            AddAction(content, "about-open-settings", "Project Settings에서 확인 기록 편집", openSettings);
            AddLabel(content, "공식 정책 출처", true);
            var urls = PolicyCopy.Urls;
            AddAction(content, "about-policy-tft", "TFT Developer Policy", () => openUrl(urls[0]));
            AddAction(content, "about-policy-general", "General Policies · API 제품 고지 출처", () => openUrl(urls[1]));
            AddAction(content, "about-policy-legal", "Legal Jibber Jabber · IP 이용 고지 출처", () => openUrl(urls[2]));
            AddLabel(content, "비공식 프로젝트 고지 · General Policies 원문", true);
            AddLabel(content, PolicyCopy.GeneralNotice);
            AddLabel(content, "한국어 요약: TFT Data Analyzer는 Riot Games의 보증을 받지 않으며 Riot의 견해를 대변하지 않습니다. 관련 상표의 권리는 Riot Games에 있습니다.");
            AddLabel(content, "IP 이용 고지 · Legal Jibber Jabber 원문", true);
            AddLabel(content, PolicyCopy.LegalNotice);
            AddLabel(content, "한국어 요약: Riot Games 소유 자산을 Legal Jibber Jabber 정책에 따라 사용한 프로젝트이며 Riot Games가 보증하거나 후원하지 않습니다.");
            AddLabel(content, "익명화와 로컬 저장", true);
            AddLabel(content, "참가자의 식별 정보를 저장하지 않은 익명 과거 관측과 집계를 Library/TftAnalyzerStore에 로컬 저장합니다. 개인 연구용 CSV는 사용자가 지정한 프로젝트 밖 경로에 저장합니다. 등록 기록은 이 로컬 연구 경로를 변경하지 않습니다.");
            AddLabel(content, "지표 해석", true);
            AddLabel(content, "평균 최종 순위, Top 4 비율, 1위 비율은 과거 최종 관측 조합의 지표입니다. 최소 표본 100을 충족한 경우에만 비율과 95% Wilson 신뢰구간을 표시합니다. 표본이 부족하면 분자·분모·비율·구간을 억제합니다.");
            AddLabel(content, "상대 배치 우위율은 같은 로비에서 A가 B보다 높은 최종 순위를 기록한 비율입니다. 표본은 비교 가능한 로비 수이며 실제 전투 승률이 아닙니다. 기물·장착 아이템·활성 시너지·증강 조합과 지역·큐·티어 기록 라벨별 범위를 구분합니다.");
            AddLabel(content, "제외 기능", true);
            AddLabel(content, "전투 시뮬레이션, 미래 승패 예측, 실시간 플레이 권고·스카우팅은 제공하지 않습니다. 과거 관측과 익명 집계 분석만 다룹니다.");
            Refresh();
        }
        public void Refresh()
        {
            if (!_disposed) _state.text = PolicyCopy.StateText(_model.Refresh());
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var action in _actions) action.Key.clicked -= action.Value;
            _actions.Clear();
        }
        private void AddAction(VisualElement parent, string name, string text, Action action)
        {
            var button = new Button(action) { name = name, text = text };
            parent.Add(button); _actions.Add(new KeyValuePair<Button, Action>(button, action));
        }
        private static Label AddLabel(VisualElement parent, string text, bool heading = false)
        {
            var label = new Label(text); label.AddToClassList("tft-wrapped-text");
            if (heading) { label.AddToClassList("tft-tab-title"); label.AddToClassList("tft-data-section"); }
            parent.Add(label); return label;
        }
    }
}
