using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Compliance;
using TFTAnalyzer.Editor.UI.Analysis;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.UI;

namespace TFTAnalyzer.Tests.Compliance
{
    public sealed class ReviewedAugmentPolicyProviderTests
    {
        private string _root;
        [SetUp] public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "TftPolicy-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestCase("C:relative")][TestCase(@"\relative")][TestCase("relative")]
        public void DriveOrRootRelativeProjectRootsAreNotGuessed(string root)
        {
            Assert.Throws<ArgumentException>(() => new ReviewedAugmentPolicyProvider(root));
        }

        [Test]
        public void EmptyApprovalListDoesNotCreateOrOpenAnyFile()
        {
            var opens = 0;
            var provider = new ReviewedAugmentPolicyProvider(_root, Array.Empty<ReviewedAugmentPolicyRegistration>(),
                path => { opens++; throw new IOException("secret-marker"); });
            var result = provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(AugmentPolicyLoadState.Missing));
            Assert.That(result.Metadata, Is.Null); Assert.That(opens, Is.Zero);
            Assert.That(Directory.Exists(_root), Is.False);
            Assert.That(new ReviewedAugmentPolicyProvider(_root).Load(AugmentPolicyFixture.Catalog(),
                AnalysisFixture.Identity(), CancellationToken.None).Metadata, Is.Null);
        }

        [TestCase("Standard", 100, MetricVisibility.Visible)]
        [TestCase("Standard", 99, MetricVisibility.InsufficientSample)]
        [TestCase("Legend", 100, MetricVisibility.PolicyBlocked)]
        [TestCase("LegendBased", 100, MetricVisibility.PolicyBlocked)]
        [TestCase("Unknown", 100, MetricVisibility.PolicyBlocked)]
        [TestCase("missing", 100, MetricVisibility.PolicyBlocked)]
        public void ReviewedSyntheticRulesKeepPolicyAndSampleBoundaries(string kind, int samples, MetricVisibility expected)
        {
            var doc = AugmentPolicyFixture.Document(kind);
            var bytes = Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));
            var provider = AugmentPolicyFixture.Provider(_root, bytes);
            var result = provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(AugmentPolicyLoadState.Loaded));
            var row = AnalysisService.BuildSnapshot(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(),
                AnalysisFixture.Observations(samples, "AUG"), new MetricPolicy(100, result.Metadata)).AllRows.Single(x => x.First.UnitIds.Contains("A"));
            Assert.That(row.Presentation.State, Is.EqualTo(expected));
            if (expected != MetricVisibility.Visible) Assert.That(row.Presentation.Ratios, Is.Empty);
        }

        [TestCase("missing-field")][TestCase("null-field")][TestCase("wrong-type")]
        [TestCase("number-string")][TestCase("fraction")][TestCase("overflow")]
        [TestCase("unknown-field")][TestCase("unknown-rule-field")][TestCase("unknown-enum")]
        [TestCase("numeric-enum")][TestCase("unknown-id")][TestCase("duplicate-id")]
        [TestCase("setId")][TestCase("setNumber")][TestCase("patchId")]
        [TestCase("sourceGameVersion")][TestCase("sourceProfile")][TestCase("contentHash")]
        [TestCase("partial-rule")][TestCase("too-many-rules")]
        public void InvalidSchemaNeverPublishesPartialMetadata(string corruption)
        {
            var doc = AugmentPolicyFixture.Document(); var catalog = (JObject)doc["catalog"];
            var rules = (JArray)doc["rules"];
            switch (corruption)
            {
                case "missing-field": doc.Remove("schemaVersion"); break;
                case "null-field": doc["rules"] = null; break;
                case "wrong-type": doc["contentHash"] = 3; break;
                case "number-string": catalog["setNumber"] = "18"; break;
                case "fraction": catalog["setNumber"] = 18.0; break;
                case "overflow": catalog["setNumber"] = (long)int.MaxValue + 1; break;
                case "unknown-field": doc["reviewed"] = true; break;
                case "unknown-rule-field": rules[0]["reviewed"] = true; break;
                case "unknown-enum": rules[0]["kind"] = "standard"; break;
                case "numeric-enum": rules[0]["kind"] = 1; break;
                case "unknown-id": rules[0]["augmentId"] = "secret-marker"; break;
                case "duplicate-id": rules.Add(new JObject { ["augmentId"] = " AUG ", ["kind"] = "Legend" }); break;
                case "contentHash": doc["contentHash"] = new string('b', 64); break;
                case "partial-rule": rules.Add(new JObject { ["augmentId"] = "AUG2" }); break;
                case "too-many-rules": doc["rules"] = new JArray(Enumerable.Range(0, 4097).Select(i => new JObject { ["augmentId"] = "AUG", ["kind"] = "Standard" })); break;
                default: catalog[corruption] = corruption == "setNumber" ? new JValue(19) : new JValue(corruption == "sourceProfile" ? "official-plus-communitydragon" : "different"); break;
            }
            AssertInvalid(Encoding.UTF8.GetBytes(doc.ToString(Formatting.None)));
        }

        [TestCase("duplicate-property")][TestCase("trailing-value")][TestCase("comment")]
        [TestCase("trailing-comma")][TestCase("single-quote")][TestCase("invalid-utf8")]
        [TestCase("oversize")][TestCase("too-deep")][TestCase("truncated")]
        [TestCase("leading-zero")][TestCase("undefined")]
        public void NonStrictOrUnboundedDocumentsAreRejected(string corruption)
        {
            var text = AugmentPolicyFixture.Document().ToString(Formatting.None);
            byte[] bytes;
            switch (corruption)
            {
                case "duplicate-property": text = text.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"); break;
                case "trailing-value": text += " {}"; break;
                case "comment": text = "/*secret-marker*/" + text; break;
                case "trailing-comma": text = text.Substring(0, text.Length - 1) + ",}"; break;
                case "single-quote": text = text.Replace("\"schemaVersion\"", "'schemaVersion'"); break;
                case "oversize": text += new string(' ', 256 * 1024); break;
                case "too-deep": text = "{\"schemaVersion\":1,\"extra\":" + new string('[', 9) + "0" + new string(']', 9) + "}"; break;
                case "truncated": text = text.Substring(0, text.Length - 3); break;
                case "leading-zero": text = text.Replace("\"schemaVersion\":1", "\"schemaVersion\":01"); break;
                case "undefined": text = text.Replace("\"schemaVersion\":1", "\"schemaVersion\":undefined"); break;
            }
            bytes = corruption == "invalid-utf8" ? new byte[] { 0xc3, 0x28 } : Encoding.UTF8.GetBytes(text);
            AssertInvalid(bytes);
        }

        [Test]
        public void HashMismatchAndReadFailureFailClosedWithoutExceptionText()
        {
            var bytes = Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Formatting.None));
            var registration = AugmentPolicyFixture.Registration(bytes);
            var provider = new ReviewedAugmentPolicyProvider(_root, new[] { registration }, path => new MemoryStream(Encoding.UTF8.GetBytes("secret-marker")));
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Invalid));
            provider = new ReviewedAugmentPolicyProvider(_root, new[] { registration }, path => throw new IOException("secret-marker"));
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None).Metadata, Is.Null);
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Invalid));
            Assert.That(new ReviewedAugmentPolicyProvider(_root, new[] { registration }).Load(AugmentPolicyFixture.Catalog(),
                AnalysisFixture.Identity(), CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Missing));
        }

        [Test]
        public void CancellationPropagatesBeforeAndDuringReadAndDisposesTheOwnedStream()
        {
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.Throws<OperationCanceledException>(() => new ReviewedAugmentPolicyProvider(_root).Load(
                    AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), cancel.Token));
            }
            var bytes = Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Formatting.None));
            using (var cancel = new CancellationTokenSource())
            {
                var stream = new CancellingStream(bytes, cancel);
                var provider = new ReviewedAugmentPolicyProvider(_root, new[] { AugmentPolicyFixture.Registration(bytes) }, path => stream);
                Assert.Throws<OperationCanceledException>(() => provider.Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), cancel.Token));
                Assert.That(stream.Disposed, Is.True);
            }
        }

        [Test]
        public void OnlyExactRegisteredCatalogAndContentMayOpenThePinnedFile()
        {
            var bytes = Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Formatting.None)); var opens = 0;
            var provider = new ReviewedAugmentPolicyProvider(_root, new[] { AugmentPolicyFixture.Registration(bytes) }, path => { opens++; return new MemoryStream(bytes); });
            var other = AnalysisFixture.Identity(19);
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(other), other, CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Missing));
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(contentHash: new string('b', 64)), AnalysisFixture.Identity(), CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Missing));
            Assert.That(opens, Is.Zero);
            Assert.That(provider.Load(AugmentPolicyFixture.Catalog(legacy: true), AnalysisFixture.Identity(), CancellationToken.None).State, Is.EqualTo(AugmentPolicyLoadState.Loaded));
            Assert.That(opens, Is.EqualTo(1));
        }

        [Test]
        public void DirectoryJunctionCannotRedirectApprovedReadOrChangeItsTarget()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows junction fixture.");
            var bytes = Encoding.UTF8.GetBytes(AugmentPolicyFixture.Document().ToString(Formatting.None));
            var external = Path.Combine(_root, "external"); Directory.CreateDirectory(external);
            var target = Path.Combine(external, ContentHasher.Sha256Hex(bytes) + ".json"); File.WriteAllBytes(target, bytes);
            var parent = Path.Combine(_root, "Assets", "TFTAnalyzer", "Data"); Directory.CreateDirectory(parent);
            var link = Path.Combine(parent, "Policy");
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                if (!process.WaitForExit(5000)) { process.Kill(); Assert.Fail("Junction setup timed out."); }
                Assert.That(process.ExitCode, Is.Zero);
            }
            try
            {
                var result = new ReviewedAugmentPolicyProvider(_root, new[] { AugmentPolicyFixture.Registration(bytes) }).Load(
                    AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
                Assert.That(result.State, Is.EqualTo(AugmentPolicyLoadState.Invalid)); Assert.That(result.Metadata, Is.Null);
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(bytes));
            }
            finally { if (Directory.Exists(link)) Directory.Delete(link); }
        }

        private void AssertInvalid(byte[] bytes)
        {
            var result = AugmentPolicyFixture.Provider(_root, bytes).Load(AugmentPolicyFixture.Catalog(), AnalysisFixture.Identity(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(AugmentPolicyLoadState.Invalid)); Assert.That(result.Metadata, Is.Null);
        }

        private sealed class CancellingStream : MemoryStream
        {
            private readonly CancellationTokenSource _cancel;
            internal bool Disposed;
            internal CancellingStream(byte[] bytes, CancellationTokenSource cancel) : base(bytes) { _cancel = cancel; }
            public override int Read(byte[] buffer, int offset, int count) { var read = base.Read(buffer, offset, count); _cancel.Cancel(); return read; }
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        }
    }

    internal static class AugmentPolicyFixture
    {
        internal static StaticCatalog Catalog(CatalogIdentity identity = null, string contentHash = null, bool legacy = false)
        {
            identity = identity ?? AnalysisFixture.Identity(); contentHash = contentHash ?? new string('a', 64);
            var manifest = legacy ? new PatchManifest(1, identity.Patch, "Synthetic", DateTime.UnixEpoch, contentHash, null, DateTime.UnixEpoch, null, 0, 0, "", "") :
                new PatchManifest(2, identity, "Synthetic", DateTime.UnixEpoch, contentHash, null, DateTime.UnixEpoch, null, 0, 0, "", "");
            return new StaticCatalog(manifest, null, null, null, new[] { new AugmentRecord("AUG", null, "", null, null, null, "", null), new AugmentRecord("AUG2", null, "", null, null, null, "", null) });
        }
        internal static JObject Document(string kind = "Standard") => new JObject
        {
            ["schemaVersion"] = 1, ["catalog"] = new JObject { ["setId"] = "TFTSet18", ["setNumber"] = 18, ["patchId"] = "18.2", ["sourceGameVersion"] = "16.18.1", ["sourceProfile"] = "official-only" },
            ["contentHash"] = new string('a', 64), ["rules"] = kind == "missing" ? new JArray() : new JArray(new JObject { ["augmentId"] = "AUG", ["kind"] = kind })
        };
        internal static ReviewedAugmentPolicyRegistration Registration(byte[] bytes) => new ReviewedAugmentPolicyRegistration(
            AnalysisFixture.Identity(), new string('a', 64), ContentHasher.Sha256Hex(bytes), new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), new[] { "https://example.invalid/synthetic-review" });
        internal static ReviewedAugmentPolicyProvider Provider(string root, byte[] bytes) => new ReviewedAugmentPolicyProvider(root,
            new[] { Registration(bytes) }, path => new MemoryStream(bytes));
    }
}
