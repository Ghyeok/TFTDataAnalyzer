using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Tests.Ingestion;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class FieldOriginBrowserTests
    {
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
