using System;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class IncompleteCandidateTabTests
    {
        private const string Sentinel = "PRIVATE_RAW_MATCH_ID_HASH_KEY_SENTINEL";

        [TestCase(IncompleteCandidateContext.UnsupportedVersion)]
        [TestCase(IncompleteCandidateContext.PatchUnconfirmed)]
        public void UnverifiedCandidatesAndEmptyArrayAreIndividuallyQualified(IncompleteCandidateContext context)
        {
            var f = new IncompleteCandidateFactory(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()), context, 1100);
            var unit = f.Unit(f.Id(CandidateIdKind.Unit, "TEST18_Unit"), f.Number(CandidateNumberKind.UnitTier, 2),
                f.Items(Array.Empty<CandidateIdField>()));
            var participant = f.Participant(1, f.Number(CandidateNumberKind.Placement, 3),
                f.Number(CandidateNumberKind.LastRound, 20), f.Units(new[] { unit }),
                f.TraitsState(CandidateFieldState.Missing), f.Augments(Array.Empty<CandidateIdField>()));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { participant }));
            var labels = Render(match, true);
            Assert.That(labels, Has.Some.Contains("순위: 3 · 후보 · 의미 미검증"));
            Assert.That(labels, Has.Some.Contains("기물: TEST18_Unit · 후보 · 의미 미검증"));
            Assert.That(labels, Has.Some.Contains("증강: 빈 배열 수신 · 의미 미검증"));
            Assert.That(labels, Has.Some.Contains("실제 경기 패치"));
            Assert.That(labels, Has.Some.Contains("정상 통계 제외"));
        }

        [TestCase(CandidateFieldState.Missing, "누락")]
        [TestCase(CandidateFieldState.Null, "null")]
        [TestCase(CandidateFieldState.InvalidFormat, "형식 오류")]
        [TestCase(CandidateFieldState.LimitExceeded, "상한 초과")]
        public void ArrayReceiptStatesAreNotDescribedAsAbsent(CandidateFieldState state, string expected)
        {
            var f = new IncompleteCandidateFactory(null, IncompleteCandidateContext.PatchUnconfirmed, 1100);
            var participant = f.Participant(1, f.Number(CandidateNumberKind.Placement, 1),
                f.Number(CandidateNumberKind.LastRound, 30), f.UnitsState(CandidateFieldState.Null),
                f.TraitsState(CandidateFieldState.Missing), f.AugmentsState(state));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { participant }));
            var labels = Render(match, false);
            Assert.That(labels, Has.Some.Contains("증강: " + expected + " · 의미 미검증"));
            Assert.That(labels, Has.None.Contains("증강 없음"));
        }

        [Test]
        public void UnavailableReferenceHidesCatalogIdsAndUnrecognizedRawStrings()
        {
            var f = new IncompleteCandidateFactory(DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity()),
                IncompleteCandidateContext.UnsupportedVersion, 1100);
            var unit = f.Unit(f.Id(CandidateIdKind.Unit, "TEST18_Unit"), f.Number(CandidateNumberKind.UnitTier, 2),
                f.Items(new[] { f.Id(CandidateIdKind.Item, Sentinel) }));
            var participant = f.Participant(1, f.Number(CandidateNumberKind.Placement, 3),
                f.Number(CandidateNumberKind.LastRound, 20), f.Units(new[] { unit }),
                f.TraitsState(CandidateFieldState.Missing), f.AugmentsState(CandidateFieldState.Missing));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { participant }));
            var labels = Render(match, false);
            Assert.That(labels, Has.Some.Contains("참고 카탈로그 사용 불가"));
            Assert.That(labels, Has.Some.Contains("순위: 3"));
            Assert.That(labels, Has.None.Contains("TEST18_Unit"));
            Assert.That(labels, Has.None.Contains(Sentinel));
        }

        private static string[] Render(IncompleteMatchCandidate match, bool catalogAvailable)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            var host = tree.Q("collection-incomplete-candidate");
            Assert.That(host, Is.Not.Null, "The incomplete candidate display host is missing.");
            var type = typeof(CollectionTab).Assembly.GetType("TFTAnalyzer.Editor.UI.Collection.IncompleteSyntheticCandidateTab");
            Assert.That(type, Is.Not.Null, "The candidate display binding is missing.");
            var binding = Activator.CreateInstance(type, host);
            var show = type.GetMethod("Show", new[] { typeof(IncompleteMatchCandidate), typeof(bool) });
            Assert.That(show, Is.Not.Null, "The candidate display entry point is missing.");
            show.Invoke(binding, new object[] { match, catalogAvailable });
            return host.Query<Label>().ToList().Select(label => label.text ?? "").ToArray();
        }
    }
}
