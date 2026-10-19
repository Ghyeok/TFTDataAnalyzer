using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Editor.Compliance;

namespace TFTAnalyzer.Tests.Compliance
{
    public sealed class ComplianceSettingsStoreTests
    {
        private string _root;
        private string Target => Path.Combine(_root, "Library", "TftAnalyzerStore", "compliance.json");
        [SetUp] public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "TftCompliance-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test] public void MissingReadDoesNotCreateDirectoryOrFile()
        {
            var result = new ComplianceSettingsStore(_root).Load();
            Assert.That(result.State, Is.EqualTo(ComplianceReadState.Missing));
            Assert.That(Directory.Exists(_root), Is.False);
        }
        [TestCase("Unverified")][TestCase("ApprovedOrAcknowledged")][TestCase("Withdrawn")]
        public void ExplicitSaveRoundTripsEachSupportedStatus(string status)
        {
            var store = new ComplianceSettingsStore(_root); store.Save(ComplianceFixture.Valid(status));
            Assert.That(File.Exists(Target), Is.True);
            var read = store.Load();
            Assert.That(read.State, Is.EqualTo(ComplianceReadState.Valid));
            Assert.That(read.Settings.registrationStatus, Is.EqualTo(status));
            Assert.That(read.Settings.acknowledgedAt, Is.EqualTo(status == "ApprovedOrAcknowledged" ? "2026-10-19T11:00:00Z" : null));
        }
        [TestCase("{")][TestCase("null")][TestCase("[]")]
        [TestCase("{\"registrationStatus\":\"future-status\"}")]
        [TestCase("{\"registrationStatus\":\"Unverified\",\"unapproved\":\"secret-sentinel\"}")]
        public void CorruptOrUnknownContractFailsClosedWithFixedCode(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.WriteAllText(Target, content);
            var result = new ComplianceSettingsStore(_root).Load();
            Assert.That(result.State, Is.EqualTo(ComplianceReadState.Invalid));
            Assert.That(result.Settings, Is.Null);
            Assert.That(result.ErrorCode, Is.EqualTo("compliance_invalid"));
        }
        [Test] public void ConstructorDefersInvalidPathAndReadReturnsUnavailable()
        {
            ComplianceSettingsStore store = null;
            Assert.DoesNotThrow(() => store = new ComplianceSettingsStore("bad\0path"));
            Assert.That(store.Load().State, Is.EqualTo(ComplianceReadState.Unavailable));
        }
        [Test] public void LockedReadReturnsUnavailableWithoutLeakingException()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.WriteAllText(Target, "private-sentinel");
            using (new FileStream(Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = new ComplianceSettingsStore(_root).Load();
                Assert.That(result.State, Is.EqualTo(ComplianceReadState.Unavailable));
                Assert.That(result.ErrorCode, Is.EqualTo("compliance_unavailable"));
            }
        }
        [Test] public void ReplacementFailurePreservesExistingFileAndRemovesStaging()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            var original = JsonConvert.SerializeObject(ComplianceFixture.Valid()); File.WriteAllText(Target, original);
            var store = new ComplianceSettingsStore(_root, (staging, target) => { throw new IOException("private-sentinel"); });
            var error = Assert.Throws<IOException>(() => store.Save(ComplianceFixture.Valid("Withdrawn")));
            Assert.That(error.Message, Is.EqualTo("compliance_save_failed"));
            Assert.That(File.ReadAllText(Target), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Target), "*.tmp"), Is.Empty);
        }
        [Test] public void InvalidSaveDoesNotReplaceExistingFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target)); File.WriteAllText(Target, "original");
            var invalid = ComplianceFixture.Valid("unknown");
            Assert.Throws<ArgumentException>(() => new ComplianceSettingsStore(_root).Save(invalid));
            Assert.That(File.ReadAllText(Target), Is.EqualTo("original"));
        }
        [Test] public void ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows link fixture.");
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            var external = Path.Combine(_root, "external.json");
            File.WriteAllText(external, JsonConvert.SerializeObject(ComplianceFixture.Valid()));
            if (!CreateSymbolicLink(Target, external, 2)) Assert.Ignore("Host does not permit unprivileged symlinks.");
            try
            {
                var store = new ComplianceSettingsStore(_root);
                Assert.That(store.Load().State, Is.EqualTo(ComplianceReadState.Unavailable));
                Assert.Throws<IOException>(() => store.Save(ComplianceFixture.Valid("Withdrawn")));
                Assert.That(File.ReadAllText(external), Does.Contain("ApprovedOrAcknowledged"));
            }
            finally { File.Delete(Target); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)] private static extern bool CreateSymbolicLink(string link, string target, int flags);

        [Test] public void JunctionAtStoreDirectoryBlocksReadAndWriteWithoutTouchingDestination()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows junction fixture.");
            var library = Path.Combine(_root, "Library"); Directory.CreateDirectory(library);
            var external = Path.Combine(_root, "external-store"); Directory.CreateDirectory(external);
            var destination = Path.Combine(external, "compliance.json");
            var original = JsonConvert.SerializeObject(ComplianceFixture.Valid()); File.WriteAllText(destination, original);
            var link = Path.Combine(library, "TftAnalyzerStore");
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); Assert.Fail("Junction fixture creation timed out."); }
                Assert.That(process.ExitCode, Is.Zero, "Temporary junction fixture must be available without changing system privileges.");
            }
            try
            {
                var store = new ComplianceSettingsStore(_root);
                Assert.That(store.Load().State, Is.EqualTo(ComplianceReadState.Unavailable));
                Assert.Throws<IOException>(() => store.Save(ComplianceFixture.Valid("Withdrawn")));
                Assert.That(File.ReadAllText(destination), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(external), Has.Length.EqualTo(1));
            }
            finally { if (Directory.Exists(link)) Directory.Delete(link); }
        }
    }
}
