using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Tests.Ingestion;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class FieldOriginBrowserTests
    {
        [Test]
        public void FieldOriginBrowser_ManyEffectsKeepLatePointersVisibleBySharingSourceText()
        {
            var original = FieldOriginIngestionTests.Official(); var sample = original.Units[0];
            var effects = new System.Collections.Generic.Dictionary<string, double>();
            for (var i = 0; i < 40; i++) effects["Stat" + i.ToString("D2")] = i;
            var unit = new UnitRecord(sample.StableId, sample.Name, sample.Cost, null, "", sample.Sources, effects: effects);
            var source = sample.Sources[1];
            var origins = System.Linq.Enumerable.Select(FieldOriginFields.Required(unit),
                pointer => new FieldOrigin("unit", unit.StableId, pointer, new[] { source }));
            var c = new StaticCatalog(original.Manifest, new[] { unit }, null, null, null, origins);
            using (var browser = new CatalogBrowserViewModel())
            {
                browser.SetCatalog(c, c.Manifest.Identity); browser.Select(unit.StableId);
                var text = browser.Selected.DetailsText;
                var start = text.IndexOf("필드별 출처:", System.StringComparison.Ordinal);
                var end = text.IndexOf("\n\n이름(한국어):", start, System.StringComparison.Ordinal);
                var block = text.Substring(start, end - start);
                Assert.That(block, Does.Contain("/effects/Stat39").And.Contain("/stableId").And.Contain("/name/enUs"));
                Assert.That(block, Does.Not.Contain("일부 내용 생략"));
                Assert.That(block.Split(new[] { source.Url }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            }
        }

        [Test]
        public void FieldOriginBrowser_ShowsFieldSourcesAndLegacyAbsenceSeparately()
        {
            var catalog = FieldOriginIngestionTests.Official();
            using (var browser = new CatalogBrowserViewModel())
            {
                browser.SetCatalog(catalog, catalog.Manifest.Identity); browser.Select("TEST18_A");
                Assert.That(browser.Selected.DetailsText, Does.Contain("필드별 출처").And.Contain("/cost").And.Contain("공식")
                    .And.Contain("https://ddragon.leagueoflegends.com/en/unit"));
                var m = catalog.Manifest;
                var legacy = new StaticCatalog(new PatchManifest(2, m.Identity, m.SetName, m.CreatedUtc, "", m.Sources,
                    m.PolicyReviewDate, m.PolicyUrls, 0, 0, "", ""), catalog.Units, catalog.Traits, catalog.Items, catalog.Augments);
                browser.SetCatalog(legacy, m.Identity); browser.Select("TEST18_A");
                Assert.That(browser.Selected.DetailsText, Does.Contain("필드별 출처 정보 없음"));
                Assert.That(browser.Selected.DetailsText, Does.Not.Contain("/cost"));
            }
        }
    }
}
