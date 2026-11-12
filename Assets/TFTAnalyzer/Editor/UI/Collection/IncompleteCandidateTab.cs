using System;
using System.Linq;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Collection
{
    /// <summary>Synthetic-only preview. Real records require verified catalog identity and hash before display.</summary>
    internal sealed class IncompleteSyntheticCandidateTab
    {
        private readonly VisualElement _host;

        public IncompleteSyntheticCandidateTab(VisualElement host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        // Real-data integration requires proof of the selected catalog identity and hash; a boolean is not proof.
        public void Show(IncompleteMatchCandidate candidate, bool referenceCatalogVerified)
        {
            Render(candidate, referenceCatalogVerified, true, null);
        }

        internal void ShowRecord(IncompleteStoredRecord record)
        {
            if (record == null || record.IsCorrupt) throw new ArgumentException("Invalid record.");
            Render(record.Candidate, record.ReferenceAvailable, false, record.ReferenceCatalog);
        }

        private void Render(IncompleteMatchCandidate candidate, bool referenceCatalogVerified, bool synthetic, StaticCatalog catalog)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            _host.Clear();
            _host.RemoveFromClassList("tft-tab-hidden");
            if (synthetic) Add("합성 예시 · 저장된 기록이 아닙니다.");
            Add("불완전 수신 기록 · 추가 확인 필요 · 정상 통계 제외");
            Add("선택한 카탈로그는 참고용이며 실제 경기 패치를 확인하지 않습니다.");
            if (!referenceCatalogVerified) Add("참고 카탈로그 사용 불가");
            Number("세트 번호", candidate.SetNumber);
            Number("큐", candidate.QueueId);
            ListStatus("참가자", candidate.Participants);
            foreach (var participant in candidate.Participants.Values)
            {
                Add("참가자 " + participant.LocalOrdinal);
                Number("순위", participant.Placement);
                Number("마지막 라운드", participant.LastRound);
                ListStatus("기물 목록", participant.Units);
                foreach (var unit in participant.Units.Values)
                {
                    Id("기물", unit.StableId, referenceCatalogVerified, catalog, synthetic);
                    Number("성급", unit.Tier);
                    ListStatus("아이템 목록", unit.Items);
                    foreach (var item in unit.Items.Values) Id("아이템", item, referenceCatalogVerified, catalog, synthetic);
                }
                ListStatus("특성 목록", participant.Traits);
                foreach (var trait in participant.Traits.Values)
                {
                    Id("특성", trait.StableId, referenceCatalogVerified, catalog, synthetic);
                    Number("특성 단계", trait.Tier);
                }
                ListStatus("증강", participant.Augments);
                foreach (var augment in participant.Augments.Values) Id("증강 항목", augment, referenceCatalogVerified, catalog, synthetic);
            }
        }

        private void Number(string name, CandidateNumberField field)
        {
            Add(name + ": " + (field.HasValue ? field.Value.Value.ToString() : State(field.State)) + Meaning(field.Meaning, field.HasValue));
        }

        private void Id(string name, CandidateIdField field, bool referenceCatalogVerified, StaticCatalog catalog, bool synthetic)
        {
            LocalizedText displayName = null, description = null;
            var found = synthetic || (referenceCatalogVerified && Find(catalog, field, out displayName, out description));
            var value = field.HasValue ? (referenceCatalogVerified && found ? field.Value : "참고 카탈로그 사용 불가") :
                field.State == CandidateFieldState.CatalogUnconfirmed && !referenceCatalogVerified
                    ? "참고 카탈로그 사용 불가" : State(field.State);
            Add(name + ": " + value + Meaning(field.Meaning, field.HasValue && referenceCatalogVerified && found));
            if (!synthetic && field.HasValue && referenceCatalogVerified && found)
            {
                Add("참고 이름: " + Clip(displayName));
                Add("참고 설명: " + Clip(description));
            }
        }

        private static bool Find(StaticCatalog catalog, CandidateIdField field, out LocalizedText name, out LocalizedText description)
        {
            name = null; description = null;
            if (catalog == null || !field.HasValue) return false;
            switch (field.Kind)
            {
                case CandidateIdKind.Unit:
                    var unit = catalog.Units.FirstOrDefault(x => x.StableId == field.Value);
                    if (unit == null) return false;
                    name = unit.Name; description = unit.Description; return true;
                case CandidateIdKind.Item:
                    var item = catalog.Items.FirstOrDefault(x => x.StableId == field.Value);
                    if (item == null) return false;
                    name = item.Name; description = item.Description; return true;
                case CandidateIdKind.Trait:
                    var trait = catalog.Traits.FirstOrDefault(x => x.StableId == field.Value);
                    if (trait == null) return false;
                    name = trait.Name; description = trait.Description; return true;
                case CandidateIdKind.Augment:
                    var augment = catalog.Augments.FirstOrDefault(x => x.StableId == field.Value);
                    if (augment == null) return false;
                    name = augment.Name; description = augment.Description; return true;
                default: return false;
            }
        }

        private static string Clip(LocalizedText text)
        {
            var value = text?.DisplayValue;
            return string.IsNullOrWhiteSpace(value) ? "자료 없음" :
                value.Length > 4096 ? value.Substring(0, 4096) + "… (일부 내용 생략)" : value;
        }

        private void ListStatus<T>(string name, CandidateList<T> field)
        {
            Add(name + ": " + State(field.State) + Meaning(field.Meaning, false));
        }

        private void Add(string text)
        {
            var label = new Label(text);
            label.AddToClassList("tft-wrapped-text");
            _host.Add(label);
        }

        private static string Meaning(CandidateMeaning meaning, bool value) =>
            meaning == CandidateMeaning.Unverified ? value ? " · 후보 · 의미 미검증" : " · 의미 미검증" : " · 확인";

        private static string State(CandidateFieldState state)
        {
            switch (state)
            {
                case CandidateFieldState.Value: return "값 있음";
                case CandidateFieldState.EmptyArray: return "빈 배열 수신";
                case CandidateFieldState.Missing: return "누락";
                case CandidateFieldState.Null: return "null";
                case CandidateFieldState.InvalidFormat: return "형식 오류";
                case CandidateFieldState.LimitExceeded: return "상한 초과";
                case CandidateFieldState.CatalogUnconfirmed: return "카탈로그 미확인";
                case CandidateFieldState.OutOfRange: return "범위 오류";
                default: return "확인 불가";
            }
        }
    }
}
