using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TFTAnalyzer.Editor.Compliance;

namespace TFTAnalyzer.Tests.Compliance
{
    internal static class ComplianceFixture
    {
        internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T12:00:00Z");
        internal static ComplianceSettings Valid(string status = "ApprovedOrAcknowledged") => new ComplianceSettings
        {
            policyReviewedOn = "2026-10-19",
            policyUrls = new[] { "https://developer.riotgames.com/docs/tft",
                "https://support-developer.riotgames.com/hc/en-us/articles/22698591841939-General-Policies",
                "https://www.riotgames.com/en/legal" },
            registrationStatus = status,
            acknowledgedAt = status == "ApprovedOrAcknowledged" ? "2026-10-19T11:00:00Z" : null
        };
    }
    public sealed class CompliancePolicyTests
    {
        [Test] public void CompleteUserAcknowledgementAllowsPublication() =>
            Assert.That(CompliancePolicy.CanPublish(ComplianceFixture.Valid(), ComplianceFixture.Now), Is.True);

        [TestCase("Unverified")][TestCase("Withdrawn")][TestCase("unknown")][TestCase(null)]
        public void OnlyAcknowledgedRegistrationCanPublish(string status) =>
            Assert.That(CompliancePolicy.CanPublish(ComplianceFixture.Valid(status), ComplianceFixture.Now), Is.False);

        [TestCase("date")][TestCase("futureDate")][TestCase("timestamp")][TestCase("futureTimestamp")]
        [TestCase("urls")][TestCase("foreignUrl")][TestCase("duplicateUrl")][TestCase("nonUtc")]
        public void MissingOrInvalidMetadataBlocksPublication(string missing)
        {
            var value = ComplianceFixture.Valid();
            switch (missing)
            {
                case "date": value.policyReviewedOn = "19/10/2026"; break;
                case "futureDate": value.policyReviewedOn = "2026-10-20"; break;
                case "timestamp": value.acknowledgedAt = null; break;
                case "futureTimestamp": value.acknowledgedAt = "2026-10-19T12:00:01Z"; break;
                case "urls": value.policyUrls = null; break;
                case "foreignUrl": value.policyUrls[0] = "https://example.org"; break;
                case "duplicateUrl": value.policyUrls[0] = value.policyUrls[1]; break;
                case "nonUtc": value.acknowledgedAt = "2026-10-19T11:00:00+09:00"; break;
            }
            Assert.That(CompliancePolicy.CanPublish(value, ComplianceFixture.Now), Is.False);
        }
        [Test] public void NullAndFutureReviewRelativeToInjectedClockBlockPublication()
        {
            Assert.That(CompliancePolicy.CanPublish(null, ComplianceFixture.Now), Is.False);
            Assert.That(CompliancePolicy.CanPublish(ComplianceFixture.Valid(), ComplianceFixture.Now.AddDays(-1)), Is.False);
        }
        [Test] public void SerializedContractContainsOnlyFourApprovedFields()
        {
            var json = JObject.FromObject(ComplianceFixture.Valid());
            Assert.That(json.Properties().Select(p => p.Name), Is.EquivalentTo(new[] {
                "policyReviewedOn", "policyUrls", "registrationStatus", "acknowledgedAt" }));
        }
    }
}
