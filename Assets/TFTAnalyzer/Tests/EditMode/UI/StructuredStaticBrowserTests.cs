using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Tests.Ingestion;

namespace TFTAnalyzer.Tests.UI
{
    public sealed class StructuredStaticBrowserTests
    {
        internal static StaticCatalog Catalog(int count = 21, int extensions = 22, bool longJson = false)
        {
            var stats = Enumerable.Range(0, count).ToDictionary(i => "S" + i.ToString("D2"), i => i == 1 ? (double?)null : i);
            var extra = Enumerable.Range(0, extensions).Select(i => new StaticExtensionField("Riot", "enUs", "/raw" + i.ToString("D2"),
                i == 0 && longJson ? JsonConvert.SerializeObject(new string('a', 4094) + "😀" + new string('b', 4500) + "TAIL-END") : i.ToString()));
            return StructuredStaticMergeTests.Catalog(false, stats, tiers: new[] { new TraitBreakpoint(null, 9, 1, null) }, unique: false, extensions: extra);
        }
        internal static object Property(object value, string name)
        {
            var property = value.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, "Missing readable structured catalog property: " + name);
            return property.GetValue(value);
        }
        internal static object[] Rows(CatalogBrowserViewModel vm, string property) => ((IEnumerable)Property(vm, property)).Cast<object>().ToArray();
        internal static void Call(CatalogBrowserViewModel vm, string name, int argument)
        {
            var method = vm.GetType().GetMethod(name); Assert.That(method, Is.Not.Null, "Missing structured catalog action: " + name);
            method.Invoke(vm, new object[] { argument });
        }
        private static CatalogBrowserViewModel Browser(StaticCatalog c)
        { var vm = new CatalogBrowserViewModel(); vm.SetCatalog(c, c.Manifest.Identity); vm.Select("TEST18_Unit"); return vm; }

        [Test]
        public void StructuredStatic_BrowserTwentyRowPagesReachLastFieldWithItsSource()
        {
            using (var vm = Browser(Catalog()))
            {
                Assert.That(Rows(vm, "StructuredRows").Length, Is.EqualTo(20));
                Assert.That((int)Property(vm, "StructuredPageCount"), Is.EqualTo(2));
                Assert.That((string)Property(Rows(vm, "StructuredRows")[0], "Value"), Is.EqualTo("0"));
                Assert.That((string)Property(Rows(vm, "StructuredRows")[1], "Value"), Is.EqualTo("자료 없음"));
                Call(vm, "SetStructuredPage", 1); var last = Rows(vm, "StructuredRows").Single();
                Assert.That((string)Property(last, "Field"), Does.Contain("/baseStats/S20"));
                Assert.That(((IEnumerable)Property(last, "Sources")).Cast<SourceStamp>().Single().Sha256, Is.EqualTo("official"));
            }
        }

        [Test]
        public void StructuredStatic_BrowserExtensionRowsAndRawChunksReachAllDataWithoutSplittingEmoji()
        {
            var c = Catalog(longJson: true);
            using (var vm = Browser(c))
            {
                Assert.That(Rows(vm, "ExtensionRows").Length, Is.EqualTo(20));
                Call(vm, "SetExtensionFieldsPage", 1); Assert.That(Rows(vm, "ExtensionRows").Length, Is.EqualTo(2));
                Call(vm, "SetExtensionFieldsPage", 0); Call(vm, "SelectStructuredRow", 0);
                var text = ""; var count = (int)Property(vm, "ExtensionPageCount"); Assert.That(count, Is.GreaterThan(1));
                for (var i = 0; i < count; i++)
                {
                    Call(vm, "SetExtensionPage", i); var chunk = (string)Property(vm, "ExtensionText");
                    Assert.That(chunk.Length, Is.LessThanOrEqualTo(4096));
                    Assert.That(chunk.Length == 0 || !char.IsHighSurrogate(chunk[chunk.Length - 1]), Is.True);
                    Assert.That(chunk.Length == 0 || !char.IsLowSurrogate(chunk[0]), Is.True); text += chunk;
                }
                Assert.That(text, Is.EqualTo(c.Units.Single().Extensions[0].CanonicalJson).And.Contain("TAIL-END"));
            }
        }

        [TestCase("search")]
        [TestCase("selection")]
        [TestCase("catalog")]
        public void StructuredStatic_BrowserInputChangesResetAllDetailPages(string change)
        {
            using (var vm = Browser(Catalog(longJson: true)))
            {
                Call(vm, "SetStructuredPage", 1); Call(vm, "SetExtensionFieldsPage", 1); Call(vm, "SelectStructuredRow", 20); Call(vm, "SetExtensionPage", 1);
                if (change == "search") vm.SetFilter(CatalogRecordKind.Unit, "other");
                if (change == "selection") vm.Select("missing");
                if (change == "catalog") { var c = Catalog(); vm.SetCatalog(c, c.Manifest.Identity); }
                Assert.That((int)Property(vm, "StructuredPage"), Is.Zero); Assert.That((int)Property(vm, "ExtensionFieldsPage"), Is.Zero);
                Assert.That((int)Property(vm, "ExtensionPage"), Is.Zero); Assert.That((string)Property(vm, "ExtensionText"), Is.Empty);
            }
        }

        [Test]
        public void StructuredStatic_BrowserShowsUnknownBoundaryAndFalseWithoutGuessingMeaning()
        {
            using (var vm = Browser(Catalog()))
            {
                vm.SetFilter(CatalogRecordKind.Trait, ""); vm.Select("TEST18_Trait");
                var minimum = Rows(vm, "StructuredRows").Single(x => ((string)Property(x, "Field")).EndsWith("/minUnits", StringComparison.Ordinal));
                Assert.That((string)Property(minimum, "Value"), Is.EqualTo("자료 없음"));
                Assert.That(((IEnumerable)Property(minimum, "Sources")).Cast<SourceStamp>().Count(), Is.EqualTo(1));
                vm.SetFilter(CatalogRecordKind.Item, ""); vm.Select("TEST18_Item");
                Assert.That((string)Property(Rows(vm, "StructuredRows").Single(), "Value"), Is.EqualTo("아니요"));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void StructuredStatic_OldStoredSchemasExplainMissingStructures(int schema)
        {
            var current = Catalog(); var m = current.Manifest;
            var manifest = schema == 1 ? new PatchManifest(1, m.Patch, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate, null, 0, 0, "", "") :
                new PatchManifest(schema, m.Identity, m.SetName, m.CreatedUtc, "", m.Sources, m.PolicyReviewDate, null, 0, 0, "", "");
            var c = new StaticCatalog(manifest, current.Units.Select(x => new UnitRecord(x.StableId, x.Name, x.Cost, null, "", x.Sources)), null, null, null);
            using (var vm = new CatalogBrowserViewModel())
            {
                vm.SetCatalog(c, m.Identity); vm.Select("TEST18_Unit");
                Assert.That(Rows(vm, "StructuredRows"), Is.Empty); Assert.That(Rows(vm, "ExtensionRows"), Is.Empty);
                Assert.That((string)Property(vm, "StructuredNotice"), Does.Contain("기존 카탈로그"));
            }
        }

        [Test]
        public void StructuredStatic_DisposedBrowserIgnoresLateDetailActions()
        {
            var vm = Browser(Catalog(longJson: true)); var changes = 0; vm.Changed += () => changes++;
            vm.Dispose(); Call(vm, "SetStructuredPage", 1); Call(vm, "SetExtensionFieldsPage", 1); Call(vm, "SelectStructuredRow", 0); Call(vm, "SetExtensionPage", 1);
            Assert.That(changes, Is.Zero); Assert.That(Rows(vm, "StructuredRows"), Is.Empty); Assert.That((string)Property(vm, "ExtensionText"), Is.Empty);
        }
    }
}
