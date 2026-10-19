using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.UI;
using TFTAnalyzer.Editor.UI.About;
using TFTAnalyzer.Tests.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Tests.Compliance
{
    public sealed class ComplianceTestHost : EditorWindow { }

    public sealed class ComplianceUiTests
    {
        private string _root;
        private OwnedEditorWindowScope _windows;
        private ComplianceSettingsProvider _provider;
        private string Target => Path.Combine(_root, "Library", "TftAnalyzerStore", "compliance.json");
        [SetUp] public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftComplianceUI-" + Guid.NewGuid().ToString("N"));
            _windows = new OwnedEditorWindowScope();
        }
        [TearDown] public void TearDown()
        {
            try { _provider?.OnDeactivate(); }
            finally
            {
                try { _windows?.Dispose(); }
                finally { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            }
        }
        private VisualElement Settings(ComplianceSettingsStore store = null)
        {
            var host = _windows.Create<ComplianceTestHost>();
            _provider = new ComplianceSettingsProvider(store ?? new ComplianceSettingsStore(_root), () => ComplianceFixture.Now);
            _provider.OnActivate("", host.rootVisualElement);
            return host.rootVisualElement;
        }
        [Test] public void OpeningSettingsDoesNotSaveOrCreateConfirmation()
        {
            var ui = Settings();
            Assert.That(ui.Q<TextField>("compliance-review-date"), Is.Not.Null);
            Assert.That(ui.Q<Toggle>("compliance-portal-confirmed").value, Is.False);
            Assert.That(File.Exists(Target), Is.False);
            Assert.That(ui.Q<Label>("compliance-saved-state").text, Does.Contain("등록 미확인"));
        }
        [Test] public void ApprovedDraftRequiresExplicitPortalConfirmationBeforeSaving()
        {
            var ui = Settings();
            ui.Q<TextField>("compliance-review-date").value = "2026-10-19";
            ui.Q<DropdownField>("compliance-registration").value = "ApprovedOrAcknowledged";
            CollectionUITestSupport.Submit(ui, "compliance-save");
            Assert.That(File.Exists(Target), Is.False);
            Assert.That(ui.Q<Label>("compliance-saved-state").text, Does.Contain("등록 미확인"));
            ui.Q<Toggle>("compliance-portal-confirmed").value = true;
            CollectionUITestSupport.Submit(ui, "compliance-save");
            var read = new ComplianceSettingsStore(_root).Load();
            Assert.That(read.State, Is.EqualTo(ComplianceReadState.Valid));
            Assert.That(read.Settings.acknowledgedAt, Is.EqualTo("2026-10-19T12:00:00.0000000Z"));
            Assert.That(ui.Q<Label>("compliance-saved-state").text, Does.Contain("등록 확인 기록"));
            Assert.That(ui.Q<Toggle>("compliance-portal-confirmed").value, Is.False);
        }
        [Test] public void SaveFailurePreservesSavedStateAndDraftWithoutLeakingException()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, JsonConvert.SerializeObject(ComplianceFixture.Valid()));
            var original = File.ReadAllText(Target);
            var ui = Settings(new ComplianceSettingsStore(_root, (s, t) => { throw new IOException("secret-sentinel"); }));
            ui.Q<DropdownField>("compliance-registration").value = "Withdrawn";
            CollectionUITestSupport.Submit(ui, "compliance-save");
            Assert.That(ui.Q<Label>("compliance-saved-state").text, Does.Contain("등록 확인 기록"));
            Assert.That(ui.Q<DropdownField>("compliance-registration").value, Is.EqualTo("Withdrawn"));
            Assert.That(ui.Q<Label>("compliance-save-result").text, Does.Contain("저장하지 못했습니다"));
            Assert.That(ui.Q<Label>("compliance-save-result").text, Does.Not.Contain("secret-sentinel"));
            Assert.That(File.ReadAllText(Target), Is.EqualTo(original));
        }
        [Test] public void WithdrawnSaveClearsAcknowledgementAndDeactivateUnsubscribesOldButton()
        {
            new ComplianceSettingsStore(_root).Save(ComplianceFixture.Valid());
            var ui = Settings();
            ui.Q<DropdownField>("compliance-registration").value = "Withdrawn";
            CollectionUITestSupport.Submit(ui, "compliance-save");
            Assert.That(new ComplianceSettingsStore(_root).Load().Settings.acknowledgedAt, Is.Null);
            var original = File.ReadAllText(Target);
            _provider.OnDeactivate();
            ui.Q<DropdownField>("compliance-registration").value = "Unverified";
            CollectionUITestSupport.Submit(ui, "compliance-save");
            Assert.That(File.ReadAllText(Target), Is.EqualTo(original));
        }
        [Test] public void AboutRefreshReadsSavedStateAndDisposeDetachesActions()
        {
            var host = _windows.Create<ComplianceTestHost>();
            var settingsOpens = 0; var urls = new System.Collections.Generic.List<string>();
            var tab = new AboutTab(host.rootVisualElement, new AboutViewModel(new ComplianceSettingsStore(_root)),
                () => settingsOpens++, url => urls.Add(url));
            try
            {
                tab.Refresh();
                Assert.That(host.rootVisualElement.Q<ScrollView>("about-scroll"), Is.Not.Null);
                Assert.That(host.rootVisualElement.Q<Label>("about-compliance-state").text, Does.Contain("등록 미확인"));
                var copy = string.Join("\n", host.rootVisualElement.Query<Label>().ToList().Select(label => label.text));
                foreach (var required in new[] { "Data Dragon", "CommunityDragon", "Riot Match API", "100", "95%", "Wilson", "상대 배치 우위율", "TFT Data Analyzer", "Riot Games", "익명", "전투 시뮬레이션" })
                    Assert.That(copy, Does.Contain(required));
                new ComplianceSettingsStore(_root).Save(ComplianceFixture.Valid()); tab.Refresh();
                Assert.That(host.rootVisualElement.Q<Label>("about-compliance-state").text, Does.Contain("등록 확인 기록"));
                foreach (var name in new[] { "about-policy-tft", "about-policy-general", "about-policy-legal" })
                    CollectionUITestSupport.Submit(host.rootVisualElement, name);
                Assert.That(urls, Is.EquivalentTo(ComplianceFixture.Valid().policyUrls));
                CollectionUITestSupport.Submit(host.rootVisualElement, "about-open-settings");
                Assert.That(settingsOpens, Is.EqualTo(1));
                tab.Dispose();
                CollectionUITestSupport.Submit(host.rootVisualElement, "about-open-settings");
                CollectionUITestSupport.Submit(host.rootVisualElement, "about-policy-tft");
                Assert.That(settingsOpens, Is.EqualTo(1)); Assert.That(urls, Has.Count.EqualTo(3));
            }
            finally { tab.Dispose(); }
        }
        [Test] public void ActualWindowTabReentryReloadsSavedStateAndRebuildDisposesOldBinding()
        {
            var window = _windows.Create<TftDataAnalyzerWindow>(CollectionUITestSupport.UseOfflineCollection);
            // Inject only the local metadata reader, keeping real window/tab transitions and bindings.
            var modelField = typeof(TftDataAnalyzerWindow).GetField("_about", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modelField, Is.Not.Null);
            modelField.SetValue(window, new AboutViewModel(new ComplianceSettingsStore(_root)));
            window.CreateGUI();
            CollectionUITestSupport.Submit(window.rootVisualElement, "about-tab-button");
            Assert.That(window.rootVisualElement.Q<Label>("about-compliance-state").text, Does.Contain("등록 미확인"));
            var oldBinding = typeof(TftDataAnalyzerWindow).GetField("_aboutBinding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            var oldStateLabel = window.rootVisualElement.Q<Label>("about-compliance-state");
            CollectionUITestSupport.Submit(window.rootVisualElement, "data-tab-button");
            new ComplianceSettingsStore(_root).Save(ComplianceFixture.Valid("Withdrawn"));
            var original = File.ReadAllBytes(Target);
            CollectionUITestSupport.Submit(window.rootVisualElement, "about-tab-button");
            Assert.That(window.rootVisualElement.Q<Label>("about-compliance-state").text, Does.Contain("등록 철회"));
            window.CreateGUI(); window.CreateGUI();
            Assert.That(window.rootVisualElement.Query<Button>("about-open-settings").ToList(), Has.Count.EqualTo(1));
            Assert.That(File.ReadAllBytes(Target), Is.EqualTo(original));
            // A disposed binding must not refresh its detached view from later state changes.
            Assert.That(oldBinding, Is.Not.SameAs(typeof(TftDataAnalyzerWindow).GetField("_aboutBinding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window)));
            new ComplianceSettingsStore(_root).Save(ComplianceFixture.Valid());
            ((AboutTab)oldBinding).Refresh();
            Assert.That(oldStateLabel.text, Does.Contain("등록 철회"));
            var bindingField = typeof(TftDataAnalyzerWindow).GetField("_aboutBinding", BindingFlags.Instance | BindingFlags.NonPublic);
            var lastBinding = (AboutTab)bindingField.GetValue(window);
            var lastLabel = window.rootVisualElement.Q<Label>("about-compliance-state");
            window.Close();
            Assert.That(bindingField.GetValue(window), Is.Null);
            lastBinding.Refresh();
            Assert.That(lastLabel.text, Does.Contain("등록 철회"));
        }
        [TestCase("Unverified")][TestCase("ApprovedOrAcknowledged")][TestCase("Withdrawn")]
        public void LocalResearchCsvRemainsAvailableAndRatiosSuppressedForAllRegistrationStates(string status)
        {
            new ComplianceSettingsStore(_root).Save(ComplianceFixture.Valid(status));
            var export = Path.Combine(Path.GetTempPath(), "TftComplianceExport-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                new TFTAnalyzer.Editor.UI.Analysis.AnalysisCsvExporter(_root).Save(export,
                    AnalysisFixture.Snapshot(99).AllRows.Take(1).Select(row => row.Presentation));
                var lines = File.ReadAllLines(export); var header = lines[0].Split(','); var cells = lines[1].Split(',');
                Assert.That(cells[0], Is.EqualTo("personal_research_local"));
                foreach (var name in new[] { "metric", "numerator", "denominator", "estimate_fraction", "lower_fraction", "upper_fraction" })
                    Assert.That(cells[Array.IndexOf(header, name)], Is.Empty, name);
            }
            finally { if (File.Exists(export)) File.Delete(export); }
        }
    }
}
