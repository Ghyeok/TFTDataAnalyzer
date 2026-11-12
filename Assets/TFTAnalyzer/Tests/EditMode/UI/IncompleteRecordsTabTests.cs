using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Collection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class IncompleteRecordsTabTests
    {
        private OwnedEditorWindowScope windows;
        private EditorWindow window;
        private VisualElement tree;
        private IncompleteRecordsTab tab;
        private List<IncompleteStoredRecord> records;
        private bool failRead;
        private string deleted;
        private bool approve;
        private StaticCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            failRead = false;
            deleted = null;
            approve = false;
            windows = new OwnedEditorWindowScope();
            try
            {
                window = windows.Create<EditorWindow>();
                tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
                tree.Q("collection-tab").RemoveFromClassList("tft-tab-hidden");
                window.rootVisualElement.Add(tree);
                var source = DataManagerTestSupport.Catalog(DataManagerTestSupport.Identity());
                catalog = new StaticCatalog(source.Manifest,
                    new[] { new UnitRecord("TEST18_Unit", new LocalizedText("합성 이름", ""), 1, null, "", null,
                        description: new LocalizedText("합성 설명", "")) }, null, null, null);
                var f = new IncompleteCandidateFactory(catalog,
                    IncompleteCandidateContext.UnsupportedVersion, 1100);
                var unit = f.Unit(f.Id(CandidateIdKind.Unit, "TEST18_Unit"), f.Number(CandidateNumberKind.UnitTier, 2),
                    f.Items(Array.Empty<CandidateIdField>()));
                var p = f.Participant(1, f.Number(CandidateNumberKind.Placement, 3), f.Number(CandidateNumberKind.LastRound, 20),
                    f.Units(new[] { unit }), f.TraitsState(CandidateFieldState.Missing), f.AugmentsState(CandidateFieldState.Missing));
                var candidate = f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100), f.Participants(new[] { p }));
                records = new List<IncompleteStoredRecord> { new IncompleteStoredRecord("private-handle", candidate, catalog),
                    new IncompleteStoredRecord("corrupt-handle", null, null) };
                tab = new IncompleteRecordsTab(tree.Q("collection-incomplete-records"),
                    () => { if (failRead) throw new Exception("private-path-and-hash"); return records; },
                    handle => { deleted = handle; records.RemoveAll(x => x.Handle == handle); return true; },
                    _ => approve);
            }
            catch { TearDown(); throw; }
        }

        [TearDown]
        public void TearDown() { try { tab?.Dispose(); } finally { windows?.Dispose(); } }

        [Test]
        public void DetailIsRevalidatedAndFailureClearsPreviouslyVisibleId()
        {
            tab.Refresh();
            CollectionUITestSupport.Submit(tree, "incomplete-record-0");
            Assert.That(Text(), Does.Contain("TEST18_Unit").And.Contain("의미 미검증")
                .And.Contain("합성 이름").And.Contain("합성 설명"));
            failRead = true;
            CollectionUITestSupport.Submit(tree, "incomplete-record-0");
            Assert.That(Text(), Does.Not.Contain("TEST18_Unit").And.Not.Contain("합성 이름").And.Not.Contain("합성 설명")
                .And.Not.Contain("private-path-and-hash")
                .And.Contain("기록을 불러오지 못했습니다"));
        }

        [Test]
        public void UnavailableReferenceAndInvalidationHideIdsAndPrivateHandles()
        {
            records[0] = new IncompleteStoredRecord("private-handle", records[0].Candidate, null);
            tab.Refresh();
            CollectionUITestSupport.Submit(tree, "incomplete-record-0");
            Assert.That(Text(), Does.Contain("참고 카탈로그 사용 불가").And.Not.Contain("TEST18_Unit")
                .And.Not.Contain("합성 이름").And.Not.Contain("합성 설명"));
            Assert.That(Text(), Does.Not.Contain("private-handle").And.Not.Contain("corrupt-handle"));
            tab.Invalidate();
            Assert.That(Text(), Does.Not.Contain("순위: 3"));
        }

        [Test]
        public void CorruptRecordRequiresApprovalAndDeletesOnlyTheSelection()
        {
            tab.Refresh();
            CollectionUITestSupport.Submit(tree, "incomplete-record-1");
            Assert.That(Text(), Does.Contain("손상"));
            CollectionUITestSupport.Submit(tree, "incomplete-delete");
            Assert.That(deleted, Is.Null);
            approve = true;
            CollectionUITestSupport.Submit(tree, "incomplete-delete");
            Assert.That(deleted, Is.EqualTo("corrupt-handle"));
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(Text(), Does.Not.Contain("corrupt-handle"));
        }

        private string Text() => string.Join("\n", tree.Query<Label>().ToList().Select(x => x.text)
            .Concat(tree.Query<Button>().ToList().Select(x => x.text)));
    }
}
