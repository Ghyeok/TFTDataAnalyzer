using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Tests.UI;
using TFTAnalyzer.Validation;
using UnityEngine;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class PublishedCatalogLoaderTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private CatalogIndex _index;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TftPublishedLoaderTests", Guid.NewGuid().ToString("N"));
            _paths = new StaticCatalogPaths(_root);
            _index = ScriptableObject.CreateInstance<CatalogIndex>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_index);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void LocalRowsKeepAllFourVersionProfileIdentitiesAndStandaloneMetadataWithoutWriting()
        {
            foreach (var version in new[] { "16.18.1", "16.18.2" })
            foreach (var profile in new[] { CatalogSourceProfile.OfficialOnly, CatalogSourceProfile.OfficialPlusCommunityDragon })
                Publish(DataManagerTestSupport.Identity(version, profile));
            var before = Snapshot();

            var rows = new PublishedCatalogLoader(_paths).Load(_index);

            Assert.That(rows, Has.Count.EqualTo(4));
            Assert.That(rows.Select(row => row.Identity).Distinct().Count(), Is.EqualTo(4));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            foreach (var row in rows)
            {
                Assert.That(row.RetrievedUtc, Is.EqualTo(new DateTime(2026, 10, 15, 9, 23, 0, DateTimeKind.Utc)));
                Assert.That(row.ValidationErrorCount, Is.Zero);
                Assert.That(row.ValidationWarningCount, Is.EqualTo(1));
                Assert.That(row.Directory, Is.EqualTo(_paths.CanonicalFinalDirectory(row.Identity)));
                Assert.That(row.ContentHashPrefix.Length, Is.InRange(8, 16));
                Assert.That(row.Entry.ContentHash, Does.StartWith(row.ContentHashPrefix));
                Assert.That(row.SourceBadge, Is.Not.Empty);
                // The writer's normalized snapshot deliberately differs from the display metadata.
                Assert.That(row.Catalog.Manifest.CreatedUtc, Is.EqualTo(DateTime.UnixEpoch));
                Assert.That(row.Catalog.Manifest.ValidationWarningCount, Is.Zero);
            }
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [TestCase("bytes")]
        [TestCase("index-hash")]
        [TestCase("provenance")]
        [TestCase("wrong-path")]
        [TestCase("validator")]
        [TestCase("missing-metadata")]
        [TestCase("negative-validation")]
        public void InvalidLocalEntryCannotHideTheHealthyRowOrBecomeSelectable(string mutation)
        {
            var healthy = DataManagerTestSupport.Identity();
            var broken = DataManagerTestSupport.Identity("16.18.2");
            Publish(healthy);
            var hash = Publish(broken, invalidCatalog: mutation == "validator");
            var directory = _paths.CanonicalFinalDirectory(broken);
            if (mutation == "bytes") File.AppendAllText(Path.Combine(directory, "catalog.json"), " ");
            if (mutation == "index-hash") _index.Upsert(broken, new string('f', 64), _paths.CatalogAssetPath(broken));
            if (mutation == "wrong-path")
            {
                var wrong = Path.Combine(_root, "Assets", "elsewhere");
                Directory.CreateDirectory(wrong);
                File.Copy(Path.Combine(directory, "catalog.json"), Path.Combine(wrong, "catalog.json"));
                File.Copy(Path.Combine(directory, "manifest.json"), Path.Combine(wrong, "manifest.json"));
                _index.Upsert(broken, hash, "Assets/elsewhere/catalog.json");
            }
            if (mutation == "provenance")
            {
                var path = Path.Combine(directory, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(path));
                manifest["sources"][0]["IsSupplemental"] = true;
                File.WriteAllText(path, manifest.ToString());
            }
            if (mutation == "missing-metadata" || mutation == "negative-validation")
            {
                var path = Path.Combine(directory, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(path));
                if (mutation == "missing-metadata") manifest.Remove("createdUtc");
                else manifest["validationWarningCount"] = -1;
                File.WriteAllText(path, manifest.ToString());
            }
            var before = Snapshot();

            var rows = new PublishedCatalogLoader(_paths).Load(_index);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.1").IsSelectable, Is.True);
            var failed = rows.Single(row => row.Entry.SourceGameVersion == "16.18.2");
            Assert.That(failed.IsSelectable, Is.False);
            Assert.That(failed.ErrorCode, Is.Not.Empty);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VerifiedLegacyCanBeReadWithUnprofiledOrExplicitOfficialEntryWithoutMigration(bool unprofiled)
        {
            var identity = DataManagerTestSupport.Identity();
            var hash = Publish(identity, legacy: true);
            var entry = new CatalogIndexEntry(identity, hash, _paths.LegacyCatalogAssetPath(identity.Patch));
            if (unprofiled) typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(entry, "");
            _index.RestoreEntries(new[] { entry });
            var before = Snapshot();

            var row = new PublishedCatalogLoader(_paths).Load(_index).Single();

            Assert.That(row.IsSelectable, Is.True);
            Assert.That(row.Identity.ProfileId, Is.EqualTo("official-only"));
            Assert.That(row.Directory, Is.EqualTo(_paths.LegacyFinalDirectory(identity.Patch)));
            Assert.That(Directory.Exists(_paths.CanonicalFinalDirectory(identity)), Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [Test]
        public void UnprofiledCanonicalEntryIsNotAssumedToBeOfficialLegacy()
        {
            var identity = DataManagerTestSupport.Identity();
            Publish(identity);
            typeof(CatalogIndexEntry).GetField("sourceProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_index.Entries.Single(), "");
            var row = new PublishedCatalogLoader(_paths).Load(_index).Single();
            Assert.That(row.IsSelectable, Is.False);
            Assert.That(row.ErrorCode, Is.Not.Empty);
        }

        /// <summary>Loads a stored comparison only when its pinned baseline reproduces all four record counts.</summary>
        [Test]
        public void StoredSummaryIsRecomputedAgainstVerifiedCatalogsWithoutChangingRowsOrFiles()
        {
            WriteSummary(PublishSummaryFixture().ToString(Formatting.None));

            var rows = LoadWithoutWriting();

            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            var baseline = rows.Single(row => row.Entry.SourceGameVersion == "16.18.1");
            var target = rows.Single(row => row.Entry.SourceGameVersion == "16.18.2");
            Assert.That(target.ChangeSummaryText, Does.Contain(baseline.DisplayIdentity)
                .And.Contain(baseline.ContentHashPrefix)
                .And.Contain("기물 +0 / 변경 1 / -0")
                .And.Contain("시너지 +0 / 변경 0 / -0")
                .And.Contain("아이템 +0 / 변경 0 / -0")
                .And.Contain("증강 +0 / 변경 0 / -0"));
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.3").ChangeSummaryText,
                Is.EqualTo("변경 요약 없음"));
        }

        [Test]
        public void StoredSummaryCannotTreatDifferentSetNumberAsItsBaseline()
        {
            var baseline = new CatalogIdentity(new PatchKey("TFTSet18", 17, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var target = DataManagerTestSupport.Identity("16.18.2");
            var baselineHash = Publish(baseline);
            var targetHash = Publish(target, unitName: "After");
            JObject Id(CatalogIdentity id, string hash, int number) => new JObject {
                ["setId"] = id.Patch.SetId, ["setNumber"] = number, ["patchId"] = id.Patch.PatchId,
                ["sourceGameVersion"] = id.Patch.SourceGameVersion, ["sourceProfile"] = id.ProfileId, ["contentHash"] = hash };
            JObject Counts(int changed) => new JObject { ["added"] = 0, ["changed"] = changed, ["removed"] = 0 };
            var forged = new JObject {
                ["version"] = 1, ["baseline"] = Id(baseline, baselineHash, 18), ["target"] = Id(target, targetHash, 18),
                ["units"] = Counts(1), ["traits"] = Counts(0), ["items"] = Counts(0), ["augments"] = Counts(0) };
            WriteSummary(forged.ToString(Formatting.None));

            var rows = LoadWithoutWriting();

            Assert.That(rows.Single(row => row.Identity.Equals(target)).ChangeSummaryText,
                Is.EqualTo("저장된 변경 요약: 기준을 찾을 수 없어 재검증할 수 없음"));
        }

        /// <summary>Rejects plausible but false standalone summaries while retaining the verified target and unrelated rows.</summary>
        [TestCase("changed-count")]
        [TestCase("added-count")]
        [TestCase("target-hash")]
        [TestCase("target-identity")]
        [TestCase("same-baseline-and-target")]
        [TestCase("different-set")]
        [TestCase("different-profile")]
        public void ForgedStoredSummaryIsNotTrustedEvenWhenCatalogBytesAreValid(string mutation)
        {
            var summary = PublishSummaryFixture();
            switch (mutation)
            {
                case "changed-count": summary["units"]["changed"] = 0; break;
                case "added-count": summary["traits"]["added"] = 1; break;
                case "target-hash": summary["target"]["contentHash"] = new string('f', 64); break;
                case "target-identity": summary["target"]["sourceGameVersion"] = "16.18.3"; break;
                case "same-baseline-and-target": summary["baseline"] = summary["target"].DeepClone(); break;
                case "different-set": summary["baseline"]["setId"] = "TFTSet19"; break;
                case "different-profile": summary["baseline"]["sourceProfile"] = "official-plus-communitydragon"; break;
            }
            WriteSummary(summary.ToString(Formatting.None));

            var rows = LoadWithoutWriting();

            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.2").ChangeSummaryText,
                Is.EqualTo("변경 요약을 읽거나 검증할 수 없음"));
            Assert.That(rows.Where(row => row.Entry.SourceGameVersion != "16.18.2")
                .All(row => row.ChangeSummaryText == "변경 요약 없음"), Is.True);
        }

        /// <summary>Isolates malformed and unsupported summary metadata from the catalog's independently verified content.</summary>
        [TestCase("legacy-text")]
        [TestCase("malformed-json")]
        [TestCase("unknown-version")]
        [TestCase("missing-category")]
        [TestCase("missing-count")]
        [TestCase("missing-baseline-identity")]
        [TestCase("duplicate-key")]
        [TestCase("duplicate-nested-key")]
        [TestCase("negative-count")]
        [TestCase("fractional-count")]
        [TestCase("excessive-count")]
        [TestCase("overflow-count")]
        [TestCase("invalid-hash")]
        [TestCase("oversized")]
        [TestCase("trailing-object")]
        [TestCase("non-string-metadata")]
        public void UnreadableSummaryHasFixedNoticeAndPreservesAllHealthyRows(string mutation)
        {
            var summary = PublishSummaryFixture();
            switch (mutation)
            {
                case "unknown-version": summary["version"] = 2; break;
                case "missing-category": summary.Remove("augments"); break;
                case "missing-count": ((JObject)summary["items"]).Remove("removed"); break;
                case "missing-baseline-identity": ((JObject)summary["baseline"]).Remove("setId"); break;
                case "negative-count": summary["units"]["removed"] = -1; break;
                case "fractional-count": summary["units"]["changed"] = 1.5; break;
                case "excessive-count": summary["units"]["added"] = 1000001; break;
                case "overflow-count": summary["units"]["added"] = long.MaxValue; break;
                case "invalid-hash": summary["baseline"]["contentHash"] = new string('g', 64); break;
            }
            var raw = summary.ToString(Formatting.None);
            switch (mutation)
            {
                case "legacy-text": raw = "<b>이전 자유 형식 요약</b>"; break;
                case "malformed-json": raw = "{\"version\":"; break;
                case "duplicate-key": raw = raw.Replace("\"version\":1", "\"version\":1,\"version\":1"); break;
                case "duplicate-nested-key": raw = raw.Replace("\"setNumber\":18", "\"setNumber\":18,\"setNumber\":18"); break;
                case "oversized": raw += new string(' ', 8193); break;
                case "trailing-object": raw += "{}"; break;
            }
            WriteSummary(mutation == "non-string-metadata" ? (JToken)summary : new JValue(raw));

            var rows = LoadWithoutWriting();

            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.2").ChangeSummaryText,
                Is.EqualTo("변경 요약을 읽거나 검증할 수 없음"));
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.3").ChangeSummaryText,
                Is.EqualTo("변경 요약 없음"));
        }

        /// <summary>Distinguishes absent summaries from invalid summaries without excluding catalogs from selection.</summary>
        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("empty")]
        public void MissingSummaryIsReportedAsAbsentRatherThanVerifiedOrInvalid(string state)
        {
            PublishSummaryFixture();
            WriteSummary(state == "missing" ? null : state == "null" ? JValue.CreateNull() : new JValue(""));

            var rows = LoadWithoutWriting();

            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(rows.All(row => row.IsSelectable), Is.True);
            Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.2").ChangeSummaryText,
                Is.EqualTo("변경 요약 없음"));
        }

        /// <summary>Does not claim a stored summary was verified when its baseline disappears, becomes invalid, or no longer matches the pinned hash.</summary>
        [TestCase("removed-from-index")]
        [TestCase("deleted-catalog")]
        [TestCase("tampered-catalog")]
        [TestCase("pinned-hash-mismatch")]
        public void UnavailableBaselineHasExplicitUnverifiedNoticeAndDoesNotHideTheTarget(string state)
        {
            var summary = PublishSummaryFixture();
            var baselinePath = Path.Combine(_paths.CanonicalFinalDirectory(DataManagerTestSupport.Identity()), "catalog.json");
            switch (state)
            {
                case "removed-from-index":
                    _index.RestoreEntries(_index.Entries.Where(entry => entry.SourceGameVersion != "16.18.1").ToArray());
                    break;
                case "deleted-catalog": File.Delete(baselinePath); break;
                case "tampered-catalog": File.AppendAllText(baselinePath, " "); break;
                case "pinned-hash-mismatch": summary["baseline"]["contentHash"] = new string('f', 64); break;
            }
            WriteSummary(summary.ToString(Formatting.None));

            var rows = LoadWithoutWriting();

            Assert.That(rows, Has.Length.EqualTo(state == "removed-from-index" ? 2 : 3));
            var target = rows.Single(row => row.Entry.SourceGameVersion == "16.18.2");
            Assert.That(target.IsSelectable, Is.True);
            Assert.That(target.ChangeSummaryText, Is.EqualTo("저장된 변경 요약: 기준을 찾을 수 없어 재검증할 수 없음"));
            var healthy = rows.Single(row => row.Entry.SourceGameVersion == "16.18.3");
            Assert.That(healthy.IsSelectable, Is.True);
            Assert.That(healthy.ChangeSummaryText, Is.EqualTo("변경 요약 없음"));
            if (state == "deleted-catalog" || state == "tampered-catalog")
                Assert.That(rows.Single(row => row.Entry.SourceGameVersion == "16.18.1").IsSelectable, Is.False);
        }

        private string Publish(CatalogIdentity identity, bool legacy = false, bool invalidCatalog = false, string unitName = "Before")
        {
            var catalog = DataManagerTestSupport.Catalog(identity, unitName, legacy: legacy);
            if (invalidCatalog) catalog = new StaticCatalog(catalog.Manifest,
                catalog.Units.Concat(catalog.Units), null, null, null);
            var writer = new CatalogAssetWriter(_paths);
            var staging = _paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(catalog, new CatalogValidator().Validate(catalog), staging);
            writer.Publish(staged, legacy ? _paths.LegacyFinalDirectory(identity.Patch) : _paths.CanonicalFinalDirectory(identity));
            writer.CleanupStaging(staging);
            _index.Upsert(identity, staged.ContentHash, legacy ? _paths.LegacyCatalogAssetPath(identity.Patch) : _paths.CatalogAssetPath(identity));
            return staged.ContentHash;
        }

        private JObject PublishSummaryFixture()
        {
            var baseline = DataManagerTestSupport.Identity();
            var target = DataManagerTestSupport.Identity("16.18.2");
            var baselineHash = Publish(baseline);
            var targetHash = Publish(target, unitName: "After");
            Publish(DataManagerTestSupport.Identity("16.18.3"), unitName: "Healthy");
            JObject Identity(CatalogIdentity id, string hash) => new JObject
            {
                ["setId"] = id.Patch.SetId, ["setNumber"] = id.Patch.SetNumber,
                ["patchId"] = id.Patch.PatchId, ["sourceGameVersion"] = id.Patch.SourceGameVersion,
                ["sourceProfile"] = id.ProfileId, ["contentHash"] = hash
            };
            JObject Counts(int changed) => new JObject { ["added"] = 0, ["changed"] = changed, ["removed"] = 0 };
            // The oracle is explicit fixture data, not the production summary generator or differ.
            return new JObject
            {
                ["version"] = 1, ["baseline"] = Identity(baseline, baselineHash), ["target"] = Identity(target, targetHash),
                ["units"] = Counts(1), ["traits"] = Counts(0), ["items"] = Counts(0), ["augments"] = Counts(0)
            };
        }

        private void WriteSummary(JToken value)
        {
            var path = Path.Combine(_paths.CanonicalFinalDirectory(DataManagerTestSupport.Identity("16.18.2")), "manifest.json");
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (value == null) manifest.Remove("previousPatchSummary");
            else manifest["previousPatchSummary"] = value;
            File.WriteAllText(path, manifest.ToString(Formatting.None));
        }

        private PatchRowViewModel[] LoadWithoutWriting()
        {
            var before = Snapshot();
            var entries = _index.Entries.ToArray();
            var rows = new PublishedCatalogLoader(_paths).Load(_index).ToArray();
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(_index.Entries, Is.EqualTo(entries));
            foreach (var row in rows.Where(row => row.IsSelectable))
                Assert.That(ContentHasher.Sha256Hex(File.ReadAllBytes(Path.Combine(row.Directory, "catalog.json"))),
                    Is.EqualTo(row.Entry.ContentHash));
            return rows;
        }

        private string[] Snapshot() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => path + ":" + ContentHasher.Sha256Hex(File.ReadAllBytes(path))).ToArray();
    }
}
