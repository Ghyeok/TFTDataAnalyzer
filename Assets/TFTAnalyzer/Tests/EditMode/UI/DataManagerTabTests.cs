using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class DataManagerTabTests
    {
        private EditorWindow _host;
        private VisualElement _tree;
        private DataManagerViewModel _vm;
        private DataManagerFakeService _service;
        private DataManagerTab _binding;
        private bool _approve;
        private int _confirmations;
        private OwnedEditorWindowScope _windows;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _windows = new OwnedEditorWindowScope();
            try
            {
                _approve = false;
                _confirmations = 0;
                _service = new DataManagerFakeService
                {
                    Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()),
                        DataManagerTestSupport.Row(DataManagerTestSupport.Identity(profile:
                            CatalogSourceProfile.OfficialPlusCommunityDragon), "After") }
                };
                _vm = new DataManagerViewModel(_service);
                _host = _windows.Create<EditorWindow>();
                Rebuild();
            }
            catch { TearDown(); throw; }
            // Binding construction renders synchronously; no delayed model change exists here.
            yield break;
        }

        [TearDown]
        public void TearDown()
        {
            try { _binding?.Dispose(); }
            finally
            {
                try { _vm?.Dispose(); }
                finally { _windows?.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator ReturnedReportIsPlainTextPagedAndNeverStartsAnAdditionalImport()
        {
            _service.Run = _ => Task.FromResult(StaticImportReportViewModelTests.Result(false,
                new TFTAnalyzer.Validation.ValidationReport(new[] { new TFTAnalyzer.Validation.ValidationIssue(
                    TFTAnalyzer.Validation.ValidationSeverity.Error, "bad", "TEST18_Unit", "<b>plain error</b>") }),
                Enumerable.Range(0, 205).Select(i => new MergeConflict("unit", "ID"+i, "name.enUs",
                    "<b>Official</b>", "Supplement", MergeResolution.OfficialWins)).ToArray()));
            _approve = true; SetInputs(); Submit("import-button");
            yield return WaitForEditorRender();
            var summary = _tree.Q<Label>("import-report-summary");
            Assert.That(summary, Is.Not.Null, "The actual UXML must expose the returned import report.");
            Assert.That(summary.text, Does.Contain("오류 1").And.Contain("공식 값 유지 205").And.Contain("게시하지 않음"));
            var details = _tree.Q("import-report-validation").Query<Label>().ToList();
            Assert.That(details.Any(x => x.text.Contains("<b>plain error</b>") && !x.enableRichText), Is.True);
            var merges = _tree.Q("import-report-merges");
            Assert.That(merges.Query<Label>().ToList(), Has.Count.EqualTo(20));
            Assert.That(_tree.Q<Label>("import-report-merge-page").text, Does.Contain("일부 상세 누락").And.Contain("1 / 10"));
            Submit("import-report-merge-next");
            Assert.That(merges.Query<Label>().ToList()[0].text, Does.Contain("ID20"));
            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            yield return _vm.CheckForUpdatesAsync(); yield return WaitForEditorRender();
            Assert.That(summary.text, Does.Contain("오류 1"));
            Rebuild();
            Assert.That(_tree.Q<Label>("import-report-merge-page").text, Does.Contain("1 / 10"));
            _binding.Dispose();
            Submit("import-report-merge-next");
            Assert.That(_service.Imports, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingPendingCancelledAndEmptyReportsHaveDifferentVisibleMeaning()
        {
            Assert.That(_tree.Q<Label>("import-report-summary").text, Does.Contain("기록이 없습니다"));
            _approve = true; SetInputs(); Submit("import-button"); yield return WaitForEditorRender();
            Assert.That(_tree.Q<Label>("import-report-summary").text, Does.Contain("오류 0").And.Contain("게시됨"));
            Assert.That(_tree.Q<Label>("import-report-validation-page").text, Is.EqualTo("0건"));
            var pending = new TaskCompletionSource<StaticImportResult>();
            _service.Run = _ => pending.Task;
            var run = _vm.ImportAsync(_ => true);
            try
            {
                yield return WaitForEditorRender();
                Assert.That(_tree.Q<Label>("import-report-summary").text,
                    Does.Contain("진행 중").And.Contain("게시 여부 미확인").And.Not.Contain("오류 0"));
                _vm.CancelImport();
            }
            finally { pending.TrySetCanceled(); }
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (!run.IsCompleted) { Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return WaitForEditorRender();
            Assert.That(_tree.Q<Label>("import-report-summary").text,
                Does.Contain("상세 보고서 없음").And.Contain("게시 여부 미확인").And.Not.Contain("진행 중"));
        }

        [UnityTest]
        public IEnumerator RealFieldsAndImportButtonRequireConsentAndResetTheVisibleOptIn()
        {
            SetInputs();
            Submit("import-button");
            yield return WaitForEditorRender();
            Assert.That(_confirmations, Is.EqualTo(1));
            Assert.That(_service.Imports, Is.Empty);
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);

            _approve = true;
            SetInputs();
            Submit("import-button");
            yield return WaitForEditorRender();
            Assert.That(_confirmations, Is.EqualTo(2));
            Assert.That(_service.Imports, Has.Count.EqualTo(1));
            Assert.That(_service.Imports[0].Identity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
            Assert.That(_service.Imports[0].Patch.SetId, Is.EqualTo("TFTSet18"));
            Assert.That(_service.Imports[0].Patch.SourceGameVersion, Is.EqualTo("16.18.1"));
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);
            Assert.That(_tree.Q<Label>("data-status").text, Is.EqualTo(_vm.StatusText));
        }

        [UnityTest]
        public IEnumerator BusyControlsAndCancelWaitForTheExistingOperationToSettle()
        {
            var pending = new TaskCompletionSource<StaticImportResult>();
            _service.Run = token => pending.Task;
            _approve = true;
            SetInputs();
            Submit("import-button");
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Importing));
            yield return WaitForEditorRender();
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<TextField>("set-id").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<ListView>("catalog-list").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Button>("compare-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Button>("cancel-button").enabledInHierarchy, Is.True);
            Submit("cancel-button");
            Assert.That(_service.LastToken.IsCancellationRequested, Is.True);
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.False);
            pending.SetCanceled();
            yield return WaitForOperationAndRender();
            Assert.That(_vm.State, Is.EqualTo(DataManagerState.Cancelled));
            Assert.That(_tree.Q<Button>("import-button").enabledInHierarchy, Is.True);
            Assert.That(_tree.Q<Toggle>("communitydragon-toggle").value, Is.False);
        }

        [UnityTest]
        public IEnumerator ListSelectionAndDirectionalDiffAppearThroughRealUIEvents()
        {
            _tree.Q<ListView>("catalog-list").SetSelection(1);
            yield return WaitForEditorRender();
            Assert.That(_vm.ActiveIdentity.ProfileId, Is.EqualTo("official-plus-communitydragon"));
            Assert.That(_tree.Q<Label>("active-catalog").text,
                Does.Contain("16.18.1").And.Contain("official-plus-communitydragon"));
            _tree.Q<DropdownField>("diff-before").index = 0;
            _tree.Q<DropdownField>("diff-after").index = 1;
            Submit("compare-button");
            yield return WaitForEditorRender();
            var diff = _tree.Q("catalog-diff");
            var text = string.Join("\n", diff.Query<Label>().ToList().Select(label => label.text));
            Assert.That(text, Does.Contain("official-only").And.Contain("official-plus-communitydragon"));
            Assert.That(diff.Query<Foldout>().ToList(), Is.Not.Empty);
            Assert.That(text, Does.Contain("name.enUs").And.Contain("Before").And.Contain("After"));
            Assert.That(_vm.Diff.Units.Changed, Has.Count.EqualTo(1));
        }

        /// <summary>Preserves tagged identity and before/after values while requiring their actual labels to render plain text.</summary>
        [UnityTest]
        public IEnumerator ComparisonIdentityAndFieldValuesRenderLiteralMarkup()
        {
            var rows = UseTaggedComparisonRows();
            Submit("compare-button");
            yield return WaitForEditorRender();
            var labels = _tree.Q("catalog-diff").Query<Label>().ToList();
            var identities = labels.Where(label => label.text == "이전: " + rows[0].DisplayIdentity ||
                label.text == "이후: " + rows[1].DisplayIdentity).ToList();
            Assert.That(identities, Has.Count.EqualTo(2));
            foreach (var kind in new[] { "Unit", "Trait", "Item", "Augment" })
            {
                var text = "name.enUs\n이전: <color=#00000000>Before " + kind + "</color>\n이후: <b>After " + kind + "</b>";
                var field = labels.Single(label => label.text == text);
                Assert.That(field.enableRichText, Is.False, kind + " before/after values must retain literal tags.");
            }
            foreach (var identity in identities)
                Assert.That(identity.enableRichText, Is.False, "Catalog identity markup must also remain literal.");
        }

        /// <summary>Checks tagged added/removed IDs and the real Toggle/Label hierarchy of changed-record Foldout titles.</summary>
        [UnityTest]
        public IEnumerator ComparisonIdsAndFoldoutToggleLabelsDisableRichText()
        {
            UseTaggedComparisonRows();
            Submit("compare-button");
            yield return WaitForEditorRender();
            var diff = _tree.Q("catalog-diff");
            var labels = diff.Query<Label>().ToList();
            var foldouts = diff.Query<Foldout>().ToList();
            Assert.That(foldouts, Has.Count.EqualTo(8), "Each of four categories has one changed-record Foldout.");
            foreach (var kind in new[] { "Unit", "Trait", "Item", "Augment" })
            {
                var added = labels.Single(label => label.text == "추가: TEST18_<color=#00000000>Added_" + kind + "</color>");
                var removed = labels.Single(label => label.text == "삭제: TEST18_<b>Removed_" + kind + "</b>");
                Assert.That(added.enableRichText, Is.False, "Added ID markup must remain literal.");
                Assert.That(removed.enableRichText, Is.False, "Removed ID markup must remain literal.");
                Assert.That(foldouts.Any(foldout => foldout.text == "TEST18_<b>Changed_" + kind + "</b>"), Is.True);
            }
            foreach (var foldout in foldouts)
            {
                var toggle = foldout.Q<Toggle>();
                Assert.That(toggle, Is.Not.Null);
                var titleLabels = toggle.Query<Label>().ToList();
                Assert.That(titleLabels.Any(label => label.text == foldout.text), Is.True,
                    "The actual Foldout Toggle must retain the unmodified title.");
                foreach (var title in titleLabels)
                    Assert.That(title.enableRichText, Is.False, "Foldout Toggle title labels must disable markup interpretation.");
            }
        }

        /// <summary>Reverses and repeats real comparisons without changing catalog bytes or restoring rich text on rebuilt elements.</summary>
        [UnityTest]
        public IEnumerator ComparisonDirectionAndRecreatedTreePreserveCatalogsAndPlainText()
        {
            var rows = UseTaggedComparisonRows();
            var originals = rows.Select(row => CanonicalCatalogSerializer.Serialize(row.Catalog)).ToArray();
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var reverse = iteration == 1;
                _tree.Q<DropdownField>("diff-before").index = reverse ? 1 : 0;
                _tree.Q<DropdownField>("diff-after").index = reverse ? 0 : 1;
                Submit("compare-button");
                yield return WaitForEditorRender();
                var field = _vm.Diff.Units.Changed.Single().Fields.Single(value => value.FieldName == "name.enUs");
                Assert.That(field.Before, Is.EqualTo(reverse ? "<b>After Unit</b>" : "<color=#00000000>Before Unit</color>"));
                Assert.That(field.After, Is.EqualTo(reverse ? "<color=#00000000>Before Unit</color>" : "<b>After Unit</b>"));
                foreach (var category in new[] { _vm.Diff.Units, _vm.Diff.Traits, _vm.Diff.Items, _vm.Diff.Augments })
                {
                    Assert.That(category.AddedIds, Has.Count.EqualTo(1));
                    Assert.That(category.RemovedIds, Has.Count.EqualTo(1));
                    Assert.That(category.Changed, Has.Count.EqualTo(1));
                }
                if (iteration == 1) Rebuild();
                for (var row = 0; row < rows.Length; row++)
                    Assert.That(CanonicalCatalogSerializer.Serialize(rows[row].Catalog), Is.EqualTo(originals[row]));
            }
            var finalLabels = _tree.Q("catalog-diff").Query<Label>().ToList();
            Assert.That(finalLabels, Is.Not.Empty);
            foreach (var label in finalLabels)
                Assert.That(label.enableRichText, Is.False, "Rebuilt comparison labels must remain plain text: " + label.text);
            Assert.That(_service.Imports, Is.Empty);
            Assert.That(_service.VersionCalls, Is.Zero);
        }

        /// <summary>Injects validator-accepted memory catalogs containing markup in all four record categories and identity text.</summary>
        private PatchRowViewModel[] UseTaggedComparisonRows()
        {
            _binding.Dispose();
            _vm.Dispose();
            var rows = new[] { TaggedComparisonRow(false), TaggedComparisonRow(true) };
            _service.Rows = rows;
            _vm = new DataManagerViewModel(_service);
            Rebuild();
            return rows;
        }

        /// <summary>Creates a fabricated catalog with one changed and one side-specific record per category without loading files.</summary>
        private static PatchRowViewModel TaggedComparisonRow(bool after)
        {
            var identity = new CatalogIdentity(new PatchKey("<b>TFTSet18</b>", 18, "18.2",
                after ? "16.18.2" : "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var manifest = DataManagerTestSupport.Catalog(identity).Manifest;
            Func<string, string[]> ids = kind => new[] { "TEST18_<b>Changed_" + kind + "</b>",
                after ? "TEST18_<color=#00000000>Added_" + kind + "</color>" : "TEST18_<b>Removed_" + kind + "</b>" };
            Func<string, LocalizedText> name = kind => new LocalizedText("정상 이름",
                after ? "<b>After " + kind + "</b>" : "<color=#00000000>Before " + kind + "</color>");
            var catalog = new StaticCatalog(manifest,
                ids("Unit").Select(id => new UnitRecord(id, name("Unit"), 1, null, "", manifest.Sources)),
                ids("Trait").Select(id => new TraitRecord(id, name("Trait"), null, null, "", manifest.Sources)),
                ids("Item").Select(id => new ItemRecord(id, name("Item"), null, null, null, "", manifest.Sources)),
                ids("Augment").Select(id => new AugmentRecord(id, name("Augment"), "Gold", null, null, null, "", manifest.Sources)));
            var validation = new CatalogValidator().Validate(catalog);
            Assert.That(validation.ErrorCount, Is.Zero, "The markup fixture must remain valid catalog input.");
            return new PatchRowViewModel(new CatalogIndexEntry(identity, new string('a', 64), "test/tagged-catalog.json"),
                identity, "test/memory-only", catalog, DataManagerTestSupport.RetrievedUtc,
                validation.ErrorCount, validation.WarningCount, "");
        }

        /// <summary>Checks structured collision display and source preservation in both directions.</summary>
        [UnityTest]
        public IEnumerator CollisionComparisonDisplaysOriginalStructuresInBothDirections()
        {
            var rows = UseStructuredComparisonRows();
            var originals = rows.Select(row => CanonicalCatalogSerializer.Serialize(row.Catalog)).ToArray();
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var reverse = iteration == 1;
                _tree.Q<DropdownField>("diff-before").index = reverse ? 1 : 0;
                _tree.Q<DropdownField>("diff-after").index = reverse ? 0 : 1;
                Submit("compare-button");
                yield return WaitForEditorRender();
                // Changing the active browser catalog and dropdowns must not reinterpret an existing diff.
                _tree.Q<ListView>("catalog-list").SetSelection(reverse ? 0 : 1);
                _tree.Q<DropdownField>("diff-before").index = reverse ? 0 : 1;
                _tree.Q<DropdownField>("diff-after").index = reverse ? 1 : 0;
                Rebuild();
                foreach (var pair in new[] { ("TEST18_Unit", _vm.Diff.Units), ("TEST18_Trait", _vm.Diff.Traits),
                    ("TEST18_Item", _vm.Diff.Items), ("TEST18_Augment", _vm.Diff.Augments) })
                {
                    foreach (var field in pair.Item2.Changed.Single().Fields)
                    {
                        Assert.That(field.Before, Is.EqualTo(field.After), "The public legacy collision strings must remain identical.");
                        var label = ComparisonField(pair.Item1, field.FieldName);
                        var values = DisplayedValues(label);
                        Assert.That(values[0], Is.Not.EqualTo(values[1]), "Original structures must remain distinguishable.");
                        if (field.FieldName.EndsWith("Ids", StringComparison.Ordinal))
                        {
                            Assert.That(values[reverse ? 1 : 0], Is.EqualTo("[\"A\\u001fB\"]"));
                            Assert.That(values[reverse ? 0 : 1], Is.EqualTo("[\"A\",\"B\"]"));
                        }
                        else if (field.FieldName == "effects")
                        {
                            Assert.That(values[reverse ? 1 : 0], Is.EqualTo("{\"A=1\\u001fB\":2}"));
                            Assert.That(values[reverse ? 0 : 1], Is.EqualTo("{\"A\":1,\"B\":2}"));
                        }
                        else if (field.FieldName == "sources")
                        {
                            var before = JArray.Parse(values[reverse ? 1 : 0]);
                            var after = JArray.Parse(values[reverse ? 0 : 1]);
                            Assert.That((string)before[0]["ETag"], Is.EqualTo("tag\u001fhash"));
                            Assert.That((string)before[0]["Sha256"], Is.EqualTo("tail"));
                            Assert.That((string)after[0]["ETag"], Is.EqualTo("tag"));
                            Assert.That((string)after[0]["Sha256"], Is.EqualTo("hash\u001ftail"));
                            Assert.That(before[0]["RetrievedUtc"], Is.Null, "Ignored retrieval time is not a compared field.");
                        }
                        Assert.That(label.enableRichText, Is.False);
                    }
                }
                for (var index = 0; index < rows.Length; index++)
                    Assert.That(CanonicalCatalogSerializer.Serialize(rows[index].Catalog), Is.EqualTo(originals[index]));
            }
        }

        /// <summary>Distinguishes control characters from literal escape sequences in displayed values.</summary>
        [UnityTest]
        public IEnumerator StructuredComparisonEscapesControlsSeparatelyFromLiteralBackslashes()
        {
            const string key = "<b>tag</b>\n\u001f\"\\n\\u001f😀";
            UseStructuredComparisonRows(new Dictionary<string, double> { [key] = 1 });
            Submit("compare-button");
            yield return WaitForEditorRender();
            var field = ComparisonField("TEST18_Unit", "effects");
            var values = DisplayedValues(field);
            Assert.That(JObject.Parse(values[0]).Properties().Single().Name, Is.EqualTo(key));
            Assert.That(values[0], Does.Contain("\\n").And.Contain("\\\\n").And.Contain("\\u001f")
                .And.Contain("\\\\u001f").And.Contain("\\\"").And.Contain("😀").And.Contain("<b>tag</b>"));
            Assert.That(values[0], Does.Not.Contain("\n").And.Not.Contain("\u001f"));
            Assert.That(field.enableRichText, Is.False);
        }

        /// <summary>Checks bounded read-only pages retain long Unicode values without splitting surrogate pairs.</summary>
        [UnityTest]
        public IEnumerator LongStructuredComparisonProvidesBoundedReadOnlyPagesWithoutSplittingSurrogates()
        {
            var key = string.Concat(Enumerable.Repeat("<b>😀</b>\\n", 1600)) + "FINAL_VALUE";
            UseStructuredComparisonRows(new Dictionary<string, double> { [key] = 1 });
            Submit("compare-button");
            yield return WaitForEditorRender();
            var preview = ComparisonField("TEST18_Unit", "effects");
            Assert.That(preview.text, Does.Contain("일부 생략"));
            Assert.That(preview.text.Length, Is.LessThan(4096));
            Assert.That(preview.enableRichText, Is.False);
            var record = _tree.Q("catalog-diff").Query<Foldout>().ToList().Single(x => x.text == "TEST18_Unit");
            var full = record.Q("diff-before-full");
            Assert.That(full, Is.Not.Null, "Every omitted character must remain accessible through bounded pages.");
            var text = full.Q<TextField>();
            Assert.That(text.isReadOnly, Is.True);
            Assert.That(text.multiline, Is.True);
            var pages = new List<string>();
            var next = full.Q<Button>("diff-value-next");
            var previous = full.Q<Button>("diff-value-previous");
            Assert.That(previous.enabledSelf, Is.False);
            for (var page = 0; ; page++)
            {
                Assert.That(page, Is.LessThan(100), "Page navigation must terminate.");
                Assert.That(text.value.Length, Is.InRange(1, 4096));
                Assert.That(char.IsLowSurrogate(text.value[0]), Is.False);
                Assert.That(char.IsHighSurrogate(text.value[text.value.Length - 1]), Is.False);
                pages.Add(text.value);
                foreach (var element in full.Query<TextElement>().ToList()) Assert.That(element.enableRichText, Is.False);
                if (!next.enabledSelf) break;
                SubmitButton(next);
            }
            Assert.That(pages.Count, Is.GreaterThan(1));
            Assert.That(JObject.Parse(string.Concat(pages)).Properties().Single().Name, Is.EqualTo(key));
            SubmitButton(previous);
            Assert.That(text.value, Is.EqualTo(pages[pages.Count - 2]));
        }

        /// <summary>Uses the same first-duplicate record policy as the comparison engine.</summary>
        [UnityTest]
        public IEnumerator StructuredComparisonUsesFirstDuplicateIdLikeTheDiffer()
        {
            // Deliberately malformed memory input: duplicate IDs cannot come from the validated disk loader.
            // This guards the differ's existing first-record policy when a selectable in-memory row is supplied.
            UseStructuredComparisonRows(duplicateFirst: true);
            Submit("compare-button");
            yield return WaitForEditorRender();
            var values = DisplayedValues(ComparisonField("TEST18_Unit", "effects"));
            Assert.That(values[0], Is.EqualTo("{\"A=1\\u001fB\":2}"));
            Assert.That(values[1], Is.EqualTo("{\"A\":1,\"B\":2}"));
            Assert.That(string.Join("\n", _tree.Q("catalog-diff").Query<Label>().ToList().Select(x => x.text)),
                Does.Not.Contain("SECOND_DUPLICATE"));
        }

        /// <summary>Shows a fixed notice when the compared source record is unavailable.</summary>
        [Test]
        public void MissingStructuredComparisonRecordUsesNoticeInsteadOfGuessingLegacyValue()
        {
            var field = new TFTAnalyzer.Diff.FieldChange("effects", "A=1\u001fB=2", "A=1\u001fB=2");
            var record = new UnitRecord("TEST18_Unit", LocalizedText.Empty, 1, null, "", null,
                effects: new Dictionary<string, double> { ["real"] = 2 });
            foreach (var missingBefore in new[] { true, false })
            {
                var display = new CatalogDiffFieldDisplay(field, missingBefore ? null : record,
                    missingBefore ? record : null);
                Assert.That(missingBefore ? display.Before : display.After,
                    Is.EqualTo("비교 당시 원본 자료를 확인할 수 없습니다."));
                Assert.That(missingBefore ? display.After : display.Before, Is.EqualTo("{\"real\":2}"));
            }
        }

        private Label ComparisonField(string id, string name) => _tree.Q("catalog-diff").Query<Foldout>()
            .ToList().Single(x => x.text == id).Query<Label>().ToList()
            .Single(x => x.text.StartsWith(name + "\n이전: ", StringComparison.Ordinal));

        private static string[] DisplayedValues(Label label) => label.text.Substring(label.text.IndexOf("\n이전: ",
            StringComparison.Ordinal) + 5).Split(new[] { "\n이후: " }, StringSplitOptions.None);

        /// <summary>Creates and selects synthetic rows for structured comparison UI checks.</summary>
        private PatchRowViewModel[] UseStructuredComparisonRows(IReadOnlyDictionary<string, double> beforeEffects = null,
            bool duplicateFirst = false)
        {
            _binding.Dispose();
            _vm.Dispose();
            var rows = new[] { StructuredComparisonRow(false, beforeEffects, duplicateFirst),
                StructuredComparisonRow(true, null, duplicateFirst) };
            _service.Rows = rows;
            _vm = new DataManagerViewModel(_service);
            Rebuild();
            return rows;
        }

        /// <summary>Builds a synthetic catalog row with controlled structured values.</summary>
        private static PatchRowViewModel StructuredComparisonRow(bool after, IReadOnlyDictionary<string, double> beforeEffects,
            bool duplicateFirst)
        {
            var identity = DataManagerTestSupport.Identity(after ? "16.18.2" : "16.18.1");
            var manifest = DataManagerTestSupport.Catalog(identity).Manifest;
            var sources = new[] { new SourceStamp("Riot Data Dragon", "https://example.invalid/static.json",
                DataManagerTestSupport.RetrievedUtc, after ? "tag" : "tag\u001fhash", after ? "hash\u001ftail" : "tail") };
            var relations = after ? new[] { "A", "B" } : new[] { "A\u001fB" };
            var effects = after ? new Dictionary<string, double> { ["A"] = 1, ["B"] = 2 } :
                beforeEffects ?? new Dictionary<string, double> { ["A=1\u001fB"] = 2 };
            var name = new LocalizedText("이름", "Name");
            var units = new List<UnitRecord> { new UnitRecord("TEST18_Unit", name, 1, relations, "", sources, effects: effects) };
            if (duplicateFirst) units.Add(new UnitRecord("TEST18_Unit", name, 1, null, "", sources,
                effects: new Dictionary<string, double> { ["SECOND_DUPLICATE"] = after ? 1 : 2 }));
            var targets = new[] { "A", "B", "A\u001fB" };
            var catalog = new StaticCatalog(manifest, units,
                new[] { new TraitRecord("TEST18_Trait", name, null, effects, "", sources) }
                    .Concat(targets.Select(id => new TraitRecord(id, name, null, null, "", manifest.Sources))),
                new[] { new ItemRecord("TEST18_Item", name, null, relations, effects, "", sources) }
                    .Concat(targets.Select(id => new ItemRecord(id, name, null, null, null, "", manifest.Sources))),
                new[] { new AugmentRecord("TEST18_Augment", name, "Gold", null, relations, effects, "", sources) });
            var validation = new CatalogValidator().Validate(catalog);
            if (!duplicateFirst) Assert.That(validation.ErrorCount, Is.Zero);
            return new PatchRowViewModel(new CatalogIndexEntry(identity, new string('a', 64), "test/structured.json"),
                identity, "test/memory-only", catalog, DataManagerTestSupport.RetrievedUtc, 0,
                validation.WarningCount, "");
        }

        [UnityTest]
        public IEnumerator DisposedBindingCannotChangeInputOrReceiveScheduledUpdatesAndRebuildKeepsActiveIdentity()
        {
            _tree.Q<ListView>("catalog-list").SetSelection(1);
            yield return WaitForEditorRender();
            var oldToggle = _tree.Q<Toggle>("communitydragon-toggle");
            var oldStatus = _tree.Q<Label>("data-status");
            var oldText = oldStatus.text;
            var pending = new TaskCompletionSource<IReadOnlyList<string>>();
            _service.Versions = token => pending.Task;
            var operation = _vm.CheckForUpdatesAsync();
            // Dispose before the coalesced update runs; then bind the same VM to a new tree.
            Rebuild();
            oldToggle.value = true;
            Assert.That(_vm.IncludeCommunityDragon, Is.False);
            pending.SetResult(new[] { "16.18.2" });
            yield return WaitForOperationAndRender();
            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(oldStatus.text, Is.EqualTo(oldText));
            Assert.That(_tree.Q<Label>("data-status").text, Is.EqualTo(_vm.StatusText));
            Assert.That(_tree.Q<Label>("active-catalog").text, Does.Contain("official-plus-communitydragon"));
            Assert.That(_service.VersionCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FewerThanTwoHealthyCatalogsShowsAnEmptyDiffStateAndDisablesComparison()
        {
            _binding.Dispose();
            _vm.Dispose();
            _service.Rows = new[] { DataManagerTestSupport.Row(DataManagerTestSupport.Identity()) };
            _vm = new DataManagerViewModel(_service);
            Rebuild();
            Assert.That(_tree.Q<Button>("compare-button").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<DropdownField>("diff-before").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<DropdownField>("diff-after").enabledInHierarchy, Is.False);
            Assert.That(_tree.Q<Label>("diff-empty-state").text, Is.Not.Empty);
            Assert.That(_service.VersionCalls, Is.Zero);
            yield break;
        }

        private void Rebuild()
        {
            _binding?.Dispose();
            _host.rootVisualElement.Clear();
            _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TFTAnalyzer/Editor/UI/TftDataAnalyzerWindow.uxml").CloneTree();
            _host.rootVisualElement.Add(_tree);
            _binding = new DataManagerTab(_tree.Q("data-tab"), _vm, summary =>
            {
                _confirmations++;
                Assert.That(summary, Does.Contain("TFTSet18").And.Contain("18.2")
                    .And.Contain("16.18.1").And.Contain("official-plus-communitydragon"));
                return _approve;
            });
        }

        private IEnumerator WaitForOperationAndRender()
        {
            var settled = !_vm.IsBusy;
            Action observe = () => settled = !_vm.IsBusy;
            _vm.Changed += observe;
            try
            {
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (!settled)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                        "The operation did not publish its settled state.");
                    yield return null;
                }
            }
            finally { _vm.Changed -= observe; }
            yield return WaitForEditorRender();
        }

        private IEnumerator WaitForEditorRender() => EditorWindowTestSupport.WaitForRender();

        private void SetInputs()
        {
            _tree.Q<TextField>("set-id").value = "TFTSet18";
            _tree.Q<IntegerField>("set-number").value = 18;
            _tree.Q<TextField>("tft-patch").value = "18.2";
            _tree.Q<TextField>("source-version").value = "16.18.1";
            _tree.Q<Toggle>("communitydragon-toggle").value = true;
        }

        private void Submit(string name)
        {
            var button = _tree.Q<Button>(name);
            Assert.That(button, Is.Not.Null);
            SubmitButton(button);
        }

        /// <summary>Submits a UI button using the same event path as a keyboard activation.</summary>
        private static void SubmitButton(Button button)
        {
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
