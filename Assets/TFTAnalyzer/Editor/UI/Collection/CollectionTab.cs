using System;
using System.Collections.Generic;
using System.Globalization;
using TFTAnalyzer.Domain;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Collection
{
    public sealed class CollectionTab : IDisposable
    {
        private readonly VisualElement _tab;
        private readonly CollectionViewModel _vm;
        private readonly Func<string, bool> _confirm;
        private readonly DropdownField _platform, _tier;
        private readonly IntegerField _queue, _maximum;
        private readonly Button _start, _cancel, _configuration;
        private bool _disposed, _queued;

        public CollectionTab(VisualElement tab, CollectionViewModel vm, Func<string, bool> confirm)
        {
            _tab = tab ?? throw new ArgumentNullException(nameof(tab));
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            _platform = tab.Q<DropdownField>("collection-platform");
            _tier = tab.Q<DropdownField>("collection-tier");
            _queue = tab.Q<IntegerField>("collection-queue");
            _maximum = tab.Q<IntegerField>("collection-maximum");
            _start = tab.Q<Button>("collection-start");
            _cancel = tab.Q<Button>("collection-cancel");
            _configuration = tab.Q<Button>("collection-configuration-check");
            _platform.choices = new List<string> { "KR", "JP1", "NA1", "BR1", "LA1", "LA2", "OC1", "EUN1", "EUW1", "TR1", "RU", "PH2", "SG2", "TH2", "TW2", "VN2" };
            _tier.choices = new List<string> { "ALL", "UNKNOWN", "UNRANKED", "IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND", "MASTER", "GRANDMASTER", "CHALLENGER", "MASTER+", "DIAMOND+" };
            _platform.SetValueWithoutNotify(vm.GetDraftPlatform(_platform.choices));
            _tier.SetValueWithoutNotify(vm.GetDraftTier(_tier.choices));
            _queue.SetValueWithoutNotify(vm.DraftQueueId);
            _maximum.SetValueWithoutNotify(vm.DraftMaximumMatches);
            _platform.RegisterValueChangedCallback(OnTextChanged);
            _tier.RegisterValueChangedCallback(OnTextChanged);
            _queue.RegisterValueChangedCallback(OnNumberChanged);
            _maximum.RegisterValueChangedCallback(OnNumberChanged);
            _start.clicked += Start;
            _cancel.clicked += Cancel;
            _configuration.clicked += Configuration;
            // Reconcile sanitized choices with the visible draft before rendering Start availability.
            CaptureInput();
            _vm.Changed += QueueRender;
            Render();
        }

        private void OnTextChanged(ChangeEvent<string> evt) => CaptureInput();
        private void OnNumberChanged(ChangeEvent<int> evt) => CaptureInput();
        private void CaptureInput()
        {
            if (!_disposed && !_vm.IsBusy)
                _vm.TrySetInput(_platform.value, _queue.value, _tier.value, _maximum.value);
        }

        private async void Start()
        {
            if (_disposed || _vm.IsBusy) return;
            CaptureInput();
            await _vm.StartAsync(_confirm);
        }
        private void Cancel() { if (!_disposed) _vm.Cancel(); }
        private void Configuration() { if (!_disposed) _vm.RefreshConfiguration(); }
        private void QueueRender()
        {
            if (_disposed || _queued) return;
            _queued = true;
            EditorApplication.update += Flush;
        }
        private void Flush()
        {
            EditorApplication.update -= Flush;
            _queued = false;
            if (!_disposed) Render();
        }

        private void Render()
        {
            var busy = _vm.IsBusy;
            _tab.Q("collection-inputs").SetEnabled(!busy);
            _start.SetEnabled(_vm.CanStart);
            _cancel.SetEnabled(busy && _vm.State != CollectionState.Cancelling);
            _configuration.SetEnabled(!busy);
            _tab.Q<Label>("collection-catalog").text = DescribeCatalog(_vm.ActiveIdentity);
            _tab.Q<Label>("collection-credentials").text = "API 키: " + (_vm.ApiKeyConfigured ? "설정됨" : "설정 필요") +
                " · 사용자 제공 시드: " + (_vm.SeedConfigured ? "설정됨" : "설정 필요");
            _tab.Q<Label>("collection-status").text = _vm.StatusText;
            var progress = _vm.Progress;
            _tab.Q<Label>("collection-counts").text = $"상세 시도 {progress.Requested} · 가져옴 {progress.Fetched} · 저장 {progress.Stored}\n" +
                $"중복 {progress.Duplicate} · 필터 제외 {progress.Filtered} · 실패 {progress.Failed}";
            _tab.Q<Label>("collection-rate-limit").text = progress.RetryAfterUtc.HasValue
                ? "요청 제한: " + progress.RetryAfterUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) + " 이후 수동 재시도"
                : "요청 제한 대기: 보고된 대기 없음";
            _tab.Q<Label>("collection-cancellation").text = _vm.State == CollectionState.Cancelling
                ? "취소: 요청됨 · 진행 중인 작업 종료 대기" : _vm.State == CollectionState.Cancelled ? "취소: 종료됨" : "취소: 요청 없음";
            _tab.Q<Label>("collection-scan-limit").text = $"ID 탐색: {progress.IdPages}페이지" +
                (progress.ScanLimitReached ? " · 탐색 상한 도달" : "");
        }

        private static string DescribeCatalog(CatalogIdentity identity)
        {
            if (identity == null) return "활성 카탈로그: 데이터 관리 탭에서 검증된 행을 선택해 주세요.";
            var patch = identity.Patch;
            return $"활성 카탈로그: {patch.SetId} (번호 {patch.SetNumber}) · TFT 패치 {patch.PatchId}\n" +
                   $"원본 게임 버전 {patch.SourceGameVersion} · 출처 {identity.ProfileId}";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vm.Changed -= QueueRender;
            EditorApplication.update -= Flush;
            _queued = false;
            _platform.UnregisterValueChangedCallback(OnTextChanged);
            _tier.UnregisterValueChangedCallback(OnTextChanged);
            _queue.UnregisterValueChangedCallback(OnNumberChanged);
            _maximum.UnregisterValueChangedCallback(OnNumberChanged);
            _start.clicked -= Start;
            _cancel.clicked -= Cancel;
            _configuration.clicked -= Configuration;
        }
    }
}
