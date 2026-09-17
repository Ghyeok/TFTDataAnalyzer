using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Ingestion.CommunityDragon;
using UnityEngine;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class CommunityDragonAdapterTests
    {
        private static readonly PatchKey Set18 =
            new PatchKey("TFTSet18", 18, "18.2", "16.18.1");

        [Test]
        public void Parse_SelectsSetNumberAndStableIdPrefix()
        {
            var catalog = new CommunityDragonAdapter().Parse(
                ReadFixture("cdragon-ko_kr.json"),
                ReadFixture("cdragon-en_us.json"),
                Set18);

            Assert.That(catalog.Units.Single().StableId, Is.EqualTo("TEST18_Unit_A"));
            Assert.That(catalog.Units.Single().Role, Is.EqualTo("Mage"));
            Assert.That(catalog.Units.Single().Effects["SupplementPower"], Is.EqualTo(25));
            Assert.That(catalog.Units,
                Has.None.Matches<UnitRecord>(unit => unit.StableId.StartsWith("TEST17_")));
        }

        [Test]
        public void Endpoints_DerivePinnedMajorMinorAndRefuseDisabledRequests()
        {
            Assert.That(
                CommunityDragonEndpoints.VersionFromDataDragon("16.18.1"),
                Is.EqualTo("16.18"));
            Assert.That(
                CommunityDragonEndpoints.Document("16.18", "ko_kr", true),
                Is.EqualTo("https://raw.communitydragon.org/16.18/cdragon/tft/ko_kr.json"));
            Assert.Throws<InvalidOperationException>(
                () => CommunityDragonEndpoints.Document("16.18", "ko_kr", false));
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "TFTAnalyzer",
                "Tests",
                "Fixtures",
                "Static",
                name));
        }
    }
}
