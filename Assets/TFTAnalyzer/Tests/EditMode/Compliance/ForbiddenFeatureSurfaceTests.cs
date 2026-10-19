using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.Compliance
{
    public sealed class ForbiddenFeatureSurfaceTests
    {
        [TestCase("Spectator")]
        [TestCase("LiveGame")]
        [TestCase("Overlay")]
        [TestCase("CombatSimulator")]
        [TestCase("WinPrediction")]
        public void ScannerRejectsExposedFeatureNames(string name) =>
            Assert.That(ForbiddenSurfaceAudit.IsForbiddenName("TFTAnalyzer.Features." + name + "Service"), Is.True);

        [TestCase("실제 전투 승률을 제공합니다.")]
        [TestCase("다음 경기 승패를 예측합니다.")]
        [TestCase("Predict your next win.")]
        [TestCase("Combat win rate: 75%")]
        [TestCase("과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다. 실제 전투 승률을 제공합니다.")]
        [TestCase("We do not predict battles. Predict your next win.")]
        public void ScannerRejectsPositiveClaimsEvenAfterAnApprovedNegativeNotice(string text) =>
            Assert.That(ForbiddenSurfaceAudit.HasForbiddenClaim(text), Is.True);

        [TestCase("과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다.")]
        [TestCase("같은 로비의 최종 순위 비교이며 실제 전투 승률이 아닙니다.")]
        [TestCase("제외 기능: 전투 시뮬레이션·미래 승패 예측")]
        [TestCase("We do not predict battles.")]
        [TestCase("상대 배치 우위율")]
        public void ScannerAllowsHistoricalMetricsAndExplicitExclusionNotices(string text) =>
            Assert.That(ForbiddenSurfaceAudit.HasForbiddenClaim(text), Is.False);

        [Test]
        public void ProductAssembliesAndMenusExposeNoForbiddenFeature()
        {
            var assemblies = new[] { typeof(StaticCatalog).Assembly, typeof(TftDataAnalyzerWindow).Assembly };
            var forbidden = new List<string>();
            foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                if ((type.IsPublic || type.IsNestedPublic) && ForbiddenSurfaceAudit.IsForbiddenName(type.FullName)) forbidden.Add(type.FullName);
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    foreach (var menu in method.GetCustomAttributes<MenuItem>())
                        if (ForbiddenSurfaceAudit.IsForbiddenName(menu.menuItem)) forbidden.Add(menu.menuItem);
            }
            Assert.That(forbidden, Is.Empty);
        }

        [Test]
        public void ActualUxmlCopyContainsNoPositiveCombatOrPredictionClaim()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            var texts = tree.Query<TextElement>().ToList().Select(value => value.text)
                .Concat(tree.Query<VisualElement>().ToList().Select(value => value.tooltip));
            Assert.That(texts.Where(text => ForbiddenSurfaceAudit.HasForbiddenClaim(text)), Is.Empty);
        }
    }

    internal static class ForbiddenSurfaceAudit
    {
        internal static bool IsForbiddenName(string name)
        {
            var normalized = Regex.Replace(name ?? "", @"[\s_/-]", "");
            return new[] { "Spectator", "LiveGame", "Overlay", "CombatSimulator", "WinPrediction" }
                .Any(feature => normalized.IndexOf(feature, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        internal static bool HasForbiddenClaim(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            // Remove only the approved negative clause; a separate positive claim still gets scanned.
            foreach (var notice in new[]
            {
                "과거 관측 결과이며 전투 예측이나 플레이 권고가 아닙니다.",
                "실제 전투 승률이 아닙니다.",
                "전투 시뮬레이션, 미래 승패 예측, 실시간 플레이 권고·스카우팅은 제공하지 않습니다.",
                "제외 기능: 전투 시뮬레이션·미래 승패 예측",
                "We do not predict battles."
            }) text = text.Replace(notice, "");
            return Regex.IsMatch(text, @"전투\s*(?:승률|시뮬레이션|예측)|(?:미래|경기|승패)[^.。!?\r\n]{0,30}예측|\bpredict(?:ion|s|ing)?\b|\bcombat\s+(?:win|simulat)|\bwin\s+prediction\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
