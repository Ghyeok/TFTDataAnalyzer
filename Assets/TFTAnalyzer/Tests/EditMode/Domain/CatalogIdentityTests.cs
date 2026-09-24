using System;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Tests.Domain
{
    public sealed class CatalogIdentityTests
    {
        [Test]
        public void DifferentSourceVersionDoesNotShareIdentity()
        {
            var first = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"), CatalogSourceProfile.OfficialOnly);
            var second = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.2"), CatalogSourceProfile.OfficialOnly);
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void DifferentProfileDoesNotShareIdentity()
        {
            var patch = new PatchKey("TFTSet18", 18, "18.2", "16.18.1");
            Assert.That(new CatalogIdentity(patch, CatalogSourceProfile.OfficialOnly),
                Is.Not.EqualTo(new CatalogIdentity(patch, CatalogSourceProfile.OfficialPlusCommunityDragon)));
        }

        [TestCase("official-only", CatalogSourceProfile.OfficialOnly)]
        [TestCase("official-plus-communitydragon", CatalogSourceProfile.OfficialPlusCommunityDragon)]
        public void StableProfileIdRoundTrips(string id, CatalogSourceProfile expected)
        {
            Assert.That(CatalogSourceProfileIds.Parse(id), Is.EqualTo(expected));
            Assert.That(CatalogSourceProfileIds.ToStableId(expected), Is.EqualTo(id));
        }

        [Test]
        public void UnknownProfileIdIsRejected()
        {
            Assert.Throws<ArgumentException>(() => CatalogSourceProfileIds.Parse("unrecognized"));
        }

        [Test]
        public void SchemaTwoCatalogSerializesProfileAndRejectsUnknownProfile()
        {
            var identity = new CatalogIdentity(new PatchKey("TFTSet18", 18, "18.2", "16.18.1"),
                CatalogSourceProfile.OfficialPlusCommunityDragon);
            var manifest = new PatchManifest(2, identity, "Test Set", DateTime.UnixEpoch, "", null,
                DateTime.UnixEpoch, null, 0, 0, "", "16.18");
            var json = CanonicalCatalogSerializer.Serialize(new StaticCatalog(manifest, null, null, null, null));

            Assert.That(json, Does.Contain("\"sourceProfile\": \"official-plus-communitydragon\""));
            Assert.That(CanonicalCatalogSerializer.Deserialize(json).Manifest.Identity, Is.EqualTo(identity));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() =>
                CanonicalCatalogSerializer.Deserialize(json.Replace("official-plus-communitydragon", "unknown")));
        }
    }
}
