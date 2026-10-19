using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Editor.Compliance;
using UnityEditor.Build;

namespace TFTAnalyzer.Tests.Compliance
{
    public sealed class ComplianceBuildGuardTests
    {
        private string _root;
        private string Target => Path.Combine(_root, "Library", "TftAnalyzerStore", "compliance.json");
        [SetUp] public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "TftBuildGate-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        [TestCase("Unverified")][TestCase("Withdrawn")][TestCase("unknown")]
        public void CallbackBlocksUnconfirmedOrUnknownStatus(string status)
        {
            Write(JsonConvert.SerializeObject(ComplianceFixture.Valid(status)));
            var guard = new ComplianceBuildGuard(new ComplianceSettingsStore(_root), () => ComplianceFixture.Now);
            var error = Assert.Throws<BuildFailedException>(() => guard.OnPreprocessBuild(null));
            Assert.That(error.Message, Does.Contain("Project/TFT Data Analyzer/Compliance"));
        }
        [Test] public void CallbackRereadsFileImmediatelyBeforeBuild()
        {
            var guard = new ComplianceBuildGuard(new ComplianceSettingsStore(_root), () => ComplianceFixture.Now);
            Write(JsonConvert.SerializeObject(ComplianceFixture.Valid()));
            Assert.DoesNotThrow(() => guard.OnPreprocessBuild(null));
            Write(JsonConvert.SerializeObject(ComplianceFixture.Valid("Withdrawn")));
            Assert.Throws<BuildFailedException>(() => guard.ValidateForBuild());
            Write("secret-sentinel{");
            var error = Assert.Throws<BuildFailedException>(() => guard.OnPreprocessBuild(null));
            Assert.That(error.Message, Does.Not.Contain("secret-sentinel"));
        }
        [Test] public void MissingAndUnavailableSettingsBlockBuild()
        {
            Assert.Throws<BuildFailedException>(() => new ComplianceBuildGuard(new ComplianceSettingsStore(_root), () => ComplianceFixture.Now).ValidateForBuild());
            Assert.Throws<BuildFailedException>(() => new ComplianceBuildGuard(new ComplianceSettingsStore("bad\0path"), () => ComplianceFixture.Now).OnPreprocessBuild(null));
        }
        [Test] public void DefaultCallbackReadsActualProjectStoreWithoutStartingPlayerBuild()
        {
            // Compare to the current project state; never alter the user's persisted metadata.
            var root = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            var read = new ComplianceSettingsStore(root).Load();
            var allowed = read.State == ComplianceReadState.Valid && CompliancePolicy.CanPublish(read.Settings, DateTimeOffset.UtcNow);
            if (allowed) Assert.DoesNotThrow(() => new ComplianceBuildGuard().OnPreprocessBuild(null));
            else Assert.Throws<BuildFailedException>(() => new ComplianceBuildGuard().OnPreprocessBuild(null));
        }
        private void Write(string content) { Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.WriteAllText(Target, content); }
    }
}
