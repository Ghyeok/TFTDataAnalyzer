using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using TFTAnalyzer.Editor.Ingestion.DataDragon;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.UI.Data;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.Ingestion;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Tests.Persistence
{
    public sealed class LocalizedCatalogPublicationTests
    {
        private string _root;private StaticCatalogPaths _paths;
        private sealed class Index : ICatalogIndexWriter
        { internal int Writes;internal bool Fail;public void Upsert(CatalogIdentity identity,string hash,string path){if(Fail)throw new IOException("Synthetic index failure");Writes++;} }
        private sealed class Source : IStaticCatalogSource,IHttpTransport
        {
            internal StaticCatalog Catalog;
            public Task<StaticSourceLoadResult> LoadAsync(StaticImportRequest request,CancellationToken token)=>Task.FromResult(new StaticSourceLoadResult(Catalog,null,null));
            public Task<HttpResponseData> GetAsync(string url,CancellationToken token)=>throw new InvalidOperationException("No network in model fixture");
        }
        [SetUp]public void Before(){_root=Path.Combine(Path.GetTempPath(),"TftLocalized_"+Guid.NewGuid().ToString("N"));_paths=new StaticCatalogPaths(_root);}
        [TearDown]public void After()
        {
            var prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(_root!=null&&Directory.Exists(_root)&&Path.GetFullPath(_root).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))Directory.Delete(_root,true);
        }
        private StaticCatalogPipeline Model(Source source,Index index)=>new StaticCatalogPipeline(source,source,_paths,new CatalogAssetWriter(_paths),new CatalogValidator(),index,null);
        private StaticCatalogPipeline Network(StructuredStaticIngestionTests.StructuredTransport transport,Index index)=>new StaticCatalogPipeline(
            new NetworkStaticCatalogSource(new DataDragonAdapter(transport),new CommunityDragonAdapter(),new CatalogMerger(),transport),transport,_paths,new CatalogAssetWriter(_paths),new CatalogValidator(),index,null);
        private static StaticImportRequest Request(bool supplement)=>new StaticImportRequest(new CatalogIdentity(CommunityDragonShapeTests.Patch,
            supplement?CatalogSourceProfile.OfficialPlusCommunityDragon:CatalogSourceProfile.OfficialOnly));
        private static StructuredStaticIngestionTests.StructuredTransport Transport()
        {
            var t=new StructuredStaticIngestionTests.StructuredTransport{AllowSupplement=true};
            StructuredStaticIngestionTests.Selected(t.Ko)["champions"][0]["name"]="";
            foreach(var d in new[]{t.Ko,t.En})
            {
                var unit=StructuredStaticIngestionTests.Selected(d)["champions"][0];
                unit["desc"]=ReferenceEquals(d,t.Ko)?"":"Description\n2026-10-27T12:00:00.000Z";
                unit["image"]=new JObject{["full"]="localized.png"};
                unit["localizations"]=new JArray(new JObject{["field"]="name",["koKr"]="MUST_NOT_OVERRIDE"});
            }
            return t;
        }

        [TestCase(false)][TestCase(true)]
        public async Task LocalizedCatalog_ActualAdaptersPublishSeparatedStringsThroughLoaderAndBrowser(bool supplement)
        {
            var t=Transport();var index=new Index();var request=Request(supplement);var result=await Network(t,index).RunAsync(request,default);
            Assert.That(result.Published,Is.True);Assert.That(result.Catalog.Manifest.SchemaVersion,Is.EqualTo(5));
            Assert.That(t.IconRequests,Is.EqualTo(1));Assert.That(index.Writes,Is.EqualTo(1));
            var saved=JObject.Parse(File.ReadAllText(Path.Combine(result.FinalDirectory,"catalog.json")));
            Assert.That(saved["localizations"].Count(),Is.EqualTo(8));
            foreach(var kind in new[]{"units","traits","items","augments"})foreach(var row in saved[kind].OfType<JObject>())
            {Assert.That(row["name"],Is.Null);Assert.That(row["description"],Is.Null);}
            Assert.That(CatalogAssetWriter.VerifyPublishedHash(result.FinalDirectory,request.Identity,false),Is.EqualTo(result.ContentHash));
            var asset=UnityEngine.ScriptableObject.CreateInstance<CatalogIndex>();
            try
            {
                asset.Upsert(request.Identity,result.ContentHash,_paths.CatalogAssetPath(request.Identity));
                var loaded=new PublishedCatalogLoader(_paths).Load(asset).Single();Assert.That(loaded.IsSelectable,Is.True);
                var c=loaded.Catalog;Assert.That(c.Units.Single().Name.UsedEnglishFallback,Is.True);
                Assert.That(c.Units.Single().Name.KoKr,Is.Empty);Assert.That(c.Units.Single().Description.EnUs,Does.Contain("\n2026-10-27T12:00:00.000Z"));
                Assert.That(c.Units.Single().BaseStats["mana"],Is.Null);Assert.That(c.Items.Single().Unique,Is.False);
                Assert.That(c.Units.Single().Extensions.Any(x=>x.Pointer=="/localizations"&&x.CanonicalJson.Contains("MUST_NOT_OVERRIDE")),Is.True);
                Assert.That(c.FieldOrigins.Single(x=>x.RecordType=="unit"&&x.Pointer=="/name/enUs").Sources.Single().IsSupplemental,Is.False);
                using(var browser=new CatalogBrowserViewModel())
                {
                    browser.SetCatalog(c,loaded.Identity);browser.Select(c.Units.Single().StableId);
                    Assert.That(browser.Selected.DetailsText,Does.Contain("한국어 없음 — 영어로 대체"));
                    Assert.That(browser.StructuredRows.Single(x=>x.Field=="/baseStats/hp").Value,Is.EqualTo("100"));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(asset);}
            Assert.That(Directory.GetDirectories(_paths.StagingDirectory),Is.Empty);
        }

        private static PatchManifest Manifest(StaticCatalog c,int schema)
        {
            var m=c.Manifest;return schema==1?new PatchManifest(1,m.Patch,m.SetName,m.CreatedUtc,"",m.Sources,m.PolicyReviewDate,m.PolicyUrls,0,0,"",""):
                new PatchManifest(schema,m.Identity,m.SetName,m.CreatedUtc,"",m.Sources,m.PolicyReviewDate,m.PolicyUrls,0,0,"",m.CommunityDragonVersion);
        }
        private static StaticCatalog Current(bool supplement,string text="Unit",double stat=100)
        {
            var a=StructuredStaticMergeTests.Catalog(false,new Dictionary<string,double?>{["hp"]=stat,["mana"]=null},new[]{new StaticNumericSeries("D",new double?[]{0,null,2})},
                new[]{new TraitBreakpoint(null,9,1,new Dictionary<string,double?>{["power"]=null})},false,
                new[]{new StaticExtensionField("Riot Data Dragon","enUs","/unmodeled","{\"token\":false}")},"Riot Data Dragon","https://ddragon.leagueoflegends.com/cdn/test.json");
            var c=supplement?new CatalogMerger().Merge(a,StructuredStaticMergeTests.Catalog(true,sourceUrl:"https://raw.communitydragon.org/16.18/cdragon/tft/en_us.json")).Catalog:a;
            return new StaticCatalog(Manifest(c,5),c.Units.Select(x=>new UnitRecord(x.StableId,new LocalizedText("",text),x.Cost,x.TraitIds,x.IconPath,x.Sources,x.Role,x.Description,x.Effects,x.BaseStats,x.AbilityVariables,x.Extensions)),c.Traits,c.Items,c.Augments,c.FieldOrigins);
        }
        // Independent old wire models; schema4 retains every existing value and origin.
        private static StaticCatalog Old(StaticCatalog c,int schema)=>schema==4?new StaticCatalog(Manifest(c,4),c.Units,c.Traits,c.Items,c.Augments,c.FieldOrigins):
            new StaticCatalog(Manifest(c,schema),
                c.Units.Select(x=>new UnitRecord(x.StableId,x.Name,x.Cost,x.TraitIds,x.IconPath,x.Sources,x.Role,x.Description,x.Effects)),
                c.Traits.Select(x=>new TraitRecord(x.StableId,x.Name,x.Description,x.Effects,x.IconPath,x.Sources)),
                c.Items.Select(x=>new ItemRecord(x.StableId,x.Name,x.Description,x.ComponentIds,x.Effects,x.IconPath,x.Sources)),c.Augments,
                schema==3?c.FieldOrigins.Where(x=>x.Pointer=="/stableId"||x.Pointer=="/name/enUs"||x.Pointer=="/cost"):null);
        private string PublishOld(StaticCatalog c,int schema)
        {
            var writer=new CatalogAssetWriter(_paths);var stage=_paths.CreateStagingDirectory();
            try
            {
                var old=Old(c,schema);var staged=writer.WriteStagedCatalog(old,new CatalogValidator().Validate(old),stage);
                var directory=schema==1?_paths.LegacyFinalDirectory(c.Manifest.Patch):_paths.CanonicalFinalDirectory(c.Manifest.Identity);
                writer.Publish(staged,directory);return directory;
            }
            finally{writer.CleanupStaging(stage);}
        }

        [TestCase(1,false)][TestCase(2,false)][TestCase(2,true)][TestCase(3,false)][TestCase(3,true)][TestCase(4,false)][TestCase(4,true)]
        public async Task LocalizedCatalog_OldOneToFourKeepsExactBytesAndActualSchema(int schema,bool supplement)
        {
            var c=Current(supplement);var directory=PublishOld(c,schema);var path=Path.Combine(directory,"catalog.json");var before=File.ReadAllBytes(path);
            var result=await Model(new Source{Catalog=c},new Index()).RunAsync(new StaticImportRequest(c.Manifest.Identity),default);
            Assert.That(result.Published&&result.Idempotent,Is.True);Assert.That(result.Catalog.Manifest.SchemaVersion,Is.EqualTo(schema));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));Assert.That(result.ContentHash,Is.EqualTo(ContentHasher.Sha256Hex(before)));
            Assert.That(result.Catalog.Manifest.Sources.Count,Is.EqualTo(c.Manifest.Sources.Count));
            Assert.That(result.Catalog.Units.Single().Sources.Count,Is.EqualTo(c.Units.Single().Sources.Count));
            if(schema==4)
            {
                Assert.That(result.Catalog.Units.Single().BaseStats["hp"],Is.EqualTo(100));Assert.That(result.Catalog.Items.Single().Unique,Is.False);
                Assert.That(result.Catalog.FieldOrigins.Count,Is.EqualTo(c.FieldOrigins.Count));
                Assert.That(result.Catalog.Units.Single().Extensions.Single().CanonicalJson,Is.EqualTo("{\"token\":false}"));
            }
            else Assert.That(result.Catalog.Units.Single().BaseStats,Is.Empty);
            Assert.That(JObject.Parse(File.ReadAllText(path))["localizations"],Is.Null);
            Assert.That(Directory.GetDirectories(_paths.StagingDirectory),Is.Empty);
        }

        [TestCase("structure")][TestCase("source")][TestCase("origin")]
        public void LocalizedCatalog_OldFourProjectionCannotHideExistingContentChanges(string change)
        {
            var c=Current(true);var directory=PublishOld(c,4);var path=Path.Combine(directory,"catalog.json");var before=File.ReadAllBytes(path);
            var d=JObject.Parse(CanonicalCatalogSerializer.Serialize(change=="structure"?Current(true,stat:200):c));
            if(change=="source")foreach(var row in d.Descendants().OfType<JObject>().Where(x=>x["isSupplemental"]?.Type==JTokenType.Boolean&&x.Value<bool>("isSupplemental")))row["sha256"]="changed";
            if(change=="origin")d["fieldOrigins"].First(x=>x.Value<string>("recordType")=="unit"&&x.Value<string>("pointer")=="/name/enUs")["sources"]=new JArray(d["manifest"]["sources"].First(x=>x.Value<bool>("isSupplemental")).DeepClone());
            var changed=CanonicalCatalogSerializer.Deserialize(d.ToString());
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await Model(new Source{Catalog=changed},new Index()).RunAsync(new StaticImportRequest(changed.Manifest.Identity),default));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));Assert.That(Directory.GetDirectories(_paths.StagingDirectory),Is.Empty);
        }

        [Test]
        public async Task LocalizedCatalog_SchemaFiveTextChangeCannotOverwritePack()
        {
            var source=new Source{Catalog=Current(false)};var pipeline=Model(source,new Index());var request=new StaticImportRequest(source.Catalog.Manifest.Identity);
            var first=await pipeline.RunAsync(request,default);var path=Path.Combine(first.FinalDirectory,"catalog.json");var before=File.ReadAllBytes(path);
            source.Catalog=Current(false,text:"Changed");
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await pipeline.RunAsync(request,default));Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
        }

        [Test]
        public async Task LocalizedCatalog_IndexFailureRecoversSameSchemaFivePack()
        {
            var t=Transport();var index=new Index{Fail=true};var pipeline=Network(t,index);var request=Request(true);
            var error=Assert.ThrowsAsync<StaticImportRecoveryException>(async()=>await pipeline.RunAsync(request,default));Assert.That(error.RecoveryState,Is.EqualTo(StaticImportRecoveryState.PublishedUnindexed));
            var before=File.ReadAllBytes(Path.Combine(error.FinalDirectory,"catalog.json"));index.Fail=false;
            var result=await pipeline.RunAsync(request,default);Assert.That(result.Idempotent,Is.True);Assert.That(result.Catalog.Manifest.SchemaVersion,Is.EqualTo(5));
            Assert.That(File.ReadAllBytes(Path.Combine(result.FinalDirectory,"catalog.json")),Is.EqualTo(before));
        }

        [Test]
        public void LocalizedCatalog_CancelBeforeLoadDoesNotPublishOrIndex()
        {
            var t=Transport();var index=new Index();
            Assert.CatchAsync<OperationCanceledException>(async()=>await Network(t,index).RunAsync(Request(true),new CancellationToken(true)));
            Assert.That(t.Requests,Is.Zero);Assert.That(index.Writes,Is.Zero);Assert.That(Directory.GetDirectories(_paths.StagingDirectory),Is.Empty);
        }
    }
}
