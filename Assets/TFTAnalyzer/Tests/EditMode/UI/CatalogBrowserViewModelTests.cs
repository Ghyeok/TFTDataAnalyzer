using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class CatalogBrowserViewModelTests
    {
        [TestCase(CatalogRecordKind.Unit, "자연", "SHARED")]
        [TestCase(CatalogRecordKind.Unit, "nature", "SHARED")]
        [TestCase(CatalogRecordKind.Unit, "shared", "SHARED")]
        [TestCase(CatalogRecordKind.Trait, "숲", "SHARED")]
        [TestCase(CatalogRecordKind.Item, "blade", "SHARED")]
        [TestCase(CatalogRecordKind.Augment, "꾸러미", "AUG")]
        public void AllKindsSearchNamesAndIdsWithoutMixingCategories(CatalogRecordKind kind, string search, string id)
        {
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(Catalog(), DataManagerTestSupport.Identity()); vm.SetFilter(kind, search);
                Assert.That(vm.TotalCount, Is.EqualTo(1));
                Assert.That(vm.Rows.Single().StableId, Is.EqualTo(id));
                vm.Select(id);
                Assert.That(vm.Selected.DetailsText, Does.Contain(id));
            }
        }

        [Test]
        public void DetailsExposeOriginalFieldsFallbackAndUnambiguousReferences()
        {
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(Catalog(), DataManagerTestSupport.Identity());
                vm.Select("SHARED");
                Assert.That(vm.Selected.DetailsText, Does.Contain("비용: 3").And.Contain("역할: Frontline"));
                Assert.That(vm.Selected.DetailsText, Does.Contain("시너지: 숲 [SHARED]"));
                vm.SetFilter(CatalogRecordKind.Item, ""); vm.Select("SHARED");
                Assert.That(vm.Selected.DetailsText, Does.Contain("아이템: Blade [SHARED]"));
                Assert.That(vm.Selected.DetailsText, Does.Contain("영어로 대체").And.Contain("Power: 1.25"));
                vm.SetFilter(CatalogRecordKind.Augment, ""); vm.Select("AUG");
                Assert.That(vm.Selected.DetailsText, Does.Contain("기물: 자연 [SHARED]").And.Contain("시너지: 숲 [SHARED]")
                    .And.Contain("아이템: Blade [SHARED]").And.Contain("MISSING").And.Contain("카탈로그 내 미수록"));
                Assert.That(vm.Selected.DetailsText, Does.Contain("등급: 자료 없음").And.Contain("Riot Data Dragon")
                    .And.Contain("https://ddragon.leagueoflegends.com/test.json").And.Contain("정규화 기본값"));
                Assert.That(vm.Selected.DetailsText, Does.Not.Contain("1970-01-01"));
            }
        }

        [TestCase(51, 2, 1)]
        [TestCase(101, 3, 1)]
        public void PagingIsStableAndEveryRecordIsReachable(int count, int pages, int lastCount)
        {
            var source = Enumerable.Range(0, count).Reverse().Select(i => new UnitRecord("U" + i.ToString("D3"),
                new LocalizedText("", "Unit " + i), 0, null, "", null)).ToArray();
            var catalog = Catalog(units: source);
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(catalog, DataManagerTestSupport.Identity());
                Assert.That(vm.PageCount, Is.EqualTo(pages)); Assert.That(vm.Rows.Count, Is.EqualTo(50));
                var ids = new List<string>();
                for (var page = 0; page < pages; page++) { vm.SetPage(page); ids.AddRange(vm.Rows.Select(x => x.StableId)); }
                Assert.That(vm.Rows.Count, Is.EqualTo(lastCount));
                Assert.That(ids, Is.EqualTo(source.Select(x => x.StableId).OrderBy(x => x, StringComparer.Ordinal)));
                vm.SetPage(int.MaxValue); Assert.That(vm.Page, Is.EqualTo(pages - 1));
                vm.SetPage(-1); Assert.That(vm.Page, Is.Zero);
                Assert.That(catalog.Units.First().StableId, Is.EqualTo(source.First().StableId));
            }
        }

        [Test]
        public void SearchCategoryAndCatalogChangesClearStaleDetailsButRepeatedSelectionDoesNot()
        {
            var catalog = Catalog();
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(catalog, DataManagerTestSupport.Identity()); vm.SetFilter(CatalogRecordKind.Unit, "자연"); vm.Select("SHARED");
                var selected = vm.Selected; var changes = 0; vm.Changed += () => changes++;
                vm.SetCatalog(catalog, DataManagerTestSupport.Identity());
                Assert.That(vm.Selected, Is.SameAs(selected)); Assert.That(vm.Search, Is.EqualTo("자연")); Assert.That(changes, Is.Zero);
                vm.SetFilter(CatalogRecordKind.Unit, "no such name");
                Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Selected, Is.Null); Assert.That(vm.StatusText, Does.Contain("검색 결과"));
                vm.Select("SHARED"); Assert.That(vm.Selected, Is.Null);
                vm.SetFilter(CatalogRecordKind.Unit, ""); vm.Select("SHARED");
                vm.SetCatalog(Catalog(), DataManagerTestSupport.Identity()); Assert.That(vm.Selected, Is.Null);
                vm.SetCatalog(null, null); Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Identity, Is.Null);
                Assert.That(vm.StatusText, Does.Contain("카탈로그"));
            }
        }

        [Test]
        public void LegacyIdentityRequiresMatchingSetVersionAndOfficialProfile()
        {
            var id = DataManagerTestSupport.Identity();
            var legacy = Catalog(legacy: true);
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(legacy, id); Assert.That(vm.Rows, Is.Not.Empty);
                foreach (var wrong in new[] {
                    new CatalogIdentity(new PatchKey("TFTSet18", 19, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly),
                    DataManagerTestSupport.Identity("16.18.2"), DataManagerTestSupport.Identity(profile: CatalogSourceProfile.OfficialPlusCommunityDragon) })
                { vm.SetCatalog(legacy, wrong); Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Identity, Is.Null); }
                vm.SetCatalog(Catalog(), id); Assert.That(vm.CatalogText, Does.Contain("18.2").And.Contain("16.18.1").And.Contain("official-only"));
            }
        }

        [Test]
        public void ProjectionIsReadOnlyBoundsExternalTextAndKeepsOriginalBytes()
        {
            var catalog = Catalog(longText: true); var before = CanonicalCatalogSerializer.Serialize(catalog);
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(catalog, DataManagerTestSupport.Identity()); vm.Select("SHARED");
                Assert.That(vm.Selected.DetailsText, Does.Contain("일부 내용 생략").And.Contain("<b>"));
                Assert.That(vm.Selected.DetailsText, Does.Not.Contain(new string('x', 4097)));
                Assert.Throws<NotSupportedException>(() => ((IList<CatalogBrowserRecord>)vm.Rows).Clear());
                vm.SetFilter(CatalogRecordKind.Unit, new string('q', 300)); Assert.That(vm.Search.Length, Is.EqualTo(256));
                Assert.That(CanonicalCatalogSerializer.Serialize(catalog), Is.EqualTo(before));
            }
        }

        [Test]
        public void MissingLocalizedValuesStayMissingAndAnExplicitZeroCostIsPreserved()
        {
            var units = new[] { new UnitRecord("EMPTY", null, 0, null, "", null) };
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(Catalog(units: units), DataManagerTestSupport.Identity()); vm.Select("EMPTY");
                Assert.That(vm.Selected.NameText, Is.EqualTo("자료 없음"));
                Assert.That(vm.Selected.DetailsText, Does.Contain("이름 표시: 자료 없음").And.Contain("설명 표시: 자료 없음")
                    .And.Contain("비용: 0").And.Contain("역할: 자료 없음"));
            }
        }

        [Test]
        public void DisposedModelDoesNotRetainRecordsOrNotify()
        {
            var vm = new CatalogBrowserViewModel(); vm.SetCatalog(Catalog(), DataManagerTestSupport.Identity()); vm.Select("SHARED");
            var changes = 0; vm.Changed += () => changes++; vm.Dispose();
            vm.SetCatalog(Catalog(), DataManagerTestSupport.Identity()); vm.SetFilter(CatalogRecordKind.Item, "Blade"); vm.SetPage(2); vm.Select("SHARED");
            Assert.That(changes, Is.Zero); Assert.That(vm.Rows, Is.Empty); Assert.That(vm.Selected, Is.Null);
        }

        internal static StaticCatalog Catalog(bool legacy = false, IEnumerable<UnitRecord> units = null, bool longText = false)
        {
            var id = DataManagerTestSupport.Identity(); var baseCatalog = DataManagerTestSupport.Catalog(id, legacy: legacy);
            var source = new[] { new SourceStamp("Riot Data Dragon", "https://ddragon.leagueoflegends.com/test.json", DateTime.UnixEpoch, "etag", new string('a', 64)) };
            var description = new LocalizedText("<b>설명</b>" + (longText ? new string('x', 5000) : ""), "English description");
            return new StaticCatalog(baseCatalog.Manifest,
                units ?? new[] { new UnitRecord("SHARED", new LocalizedText("자연", "Nature"), 3, new[] { "SHARED" }, "", source, "Frontline", description) },
                new[] { new TraitRecord("SHARED", new LocalizedText("숲", "Forest"), description, null, "", source) },
                new[] { new ItemRecord("SHARED", new LocalizedText("", "Blade"), description, new[] { "SHARED" },
                    new Dictionary<string, double> { ["Power"] = 1.25 }, "", source) },
                new[] { new AugmentRecord("AUG", new LocalizedText("꾸러미", "Bag"), "", description, new[] { "SHARED", "MISSING" }, null, "", source) });
        }
    }
}
