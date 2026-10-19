using System;
using System.Globalization;
using System.Linq;
using TFTAnalyzer.Analytics;

namespace TFTAnalyzer.Editor.UI.Analysis
{
    public sealed class MetricRowViewModel
    {
        public const string PairwiseLabel = "상대 배치 우위율";
        public const string PairwiseTooltip = "같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.";
        public MetricPresentation Presentation { get; }
        public CompositionDescriptor First { get; }
        public CompositionDescriptor Second { get; }
        public string DisplayText => CompositionText + " | " + ScopeText + " | 표본 " + Presentation.SampleCount.ToString(CultureInfo.InvariantCulture) + " | " + StateText + " | " + AverageText + " | " + RatioText;
        public string CompositionText => Describe(First) + (Second == null ? "" : " → " + Describe(Second));
        public string ScopeText => Presentation.Scope.ActiveCatalog.Patch.PatchId + " / " + Presentation.Scope.Region + " / " + Presentation.Scope.QueueId + " / " + Presentation.Scope.TierBucket;
        public string StateText => Presentation.State == MetricVisibility.Visible ? "표시 가능" :
            Presentation.State == MetricVisibility.InsufficientSample ? "표본 부족" : "정책 제한 (" + PolicyNote(Presentation.ReasonCode) + ")";
        public string AverageText => Presentation.AveragePlacement.HasValue ? "평균 최종 배치 " + Presentation.AveragePlacement.Value.ToString("0.00", CultureInfo.InvariantCulture) : "평균 최종 배치 —";
        public string RatioText => string.Join(" | ", Presentation.Ratios.Select(ratio => Label(ratio.Key) + " " +
            (ratio.Rate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% (" + ratio.Numerator.ToString(CultureInfo.InvariantCulture) + "/" +
            ratio.Denominator.ToString(CultureInfo.InvariantCulture) + "; 95% 구간 " + (ratio.Interval.Lower * 100).ToString("0.0", CultureInfo.InvariantCulture) +
            "–" + (ratio.Interval.Upper * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%)"));
        internal MetricRowViewModel(MetricPresentation presentation, CompositionDescriptor first, CompositionDescriptor second = null)
        { Presentation = presentation; First = first; Second = second; }
        private static string Describe(CompositionDescriptor board) => board.SummaryText + " [" + board.Fingerprint.Substring(0, 8) + "]";
        private static string Label(string key) => key == "top_four_rate" ? "상위 4위 비율" : key == "first_place_rate" ? "1위 비율" : PairwiseLabel;
        internal static string PolicyNote(string reason)
        {
            switch (reason)
            {
                case "insufficient_sample": return "최소 표본 미달";
                case "unknown_augment": return "카탈로그에 없는 증강";
                case "unclassified_augment": return "검수된 증강 분류 없음";
                case "legend": return "레전드 제외";
                case "legend_based_augment": return "레전드 기반 증강 제외";
                case "catalog_mismatch": return "카탈로그 불일치";
                case "policy_catalog_mismatch": return "분류 카탈로그 불일치";
                case "invalid_metric_scope": return "허용 범위 아님";
                default: return "정책에 따른 제한";
            }
        }
    }
}
