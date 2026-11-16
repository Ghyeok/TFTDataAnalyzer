using System;
using System.Reflection;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Editor.Diagnostics;
using TFTAnalyzer.Editor.RiotApi;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 1: the de-identifying version signature conversion table.</summary>
    public sealed class VersionShapeSignatureTests
    {
        [TestCase("버전 십사", "aa_aa", "none")]
        [TestCase("Версия 14.1", "{x:6-8}_99.9", "14.1")]
        [TestCase("١٤.١", "99.9", "#2.#1")]
        [TestCase("🎮14.1", "?99.9", "14.1")]
        [TestCase("16.18.123.456", "99.99.999.999", "16.18")]
        [TestCase("Version 16.18.123.456 (FAKE-BUILD-NOTE)", "{x:6-8}_99.99.999.999_({x:9-16})", "16.18")]
        [TestCase("a3f9c2d17b4e8a6f0c5d9e2b1a7f4c8d3e6b0a9f", "{x:33-64}", "none")]
        [TestCase("123e4567-e89b-12d3-a456-426614174000", "{x:33-64}", "#12")]
        [TestCase("ab-cd", "{x:1-5}", "none")]
        [TestCase("192.168.0.1", "999.999.9.9", "192.168")]
        [TestCase("Version 16.18 10.200.30.4", "{x:6-8}_99.99_99.999.99.9", "16.18")]
        [TestCase("1234567890", "{x:9-16}", "#10")]
        [TestCase("1234567890.1", "{x:9-16}.9", "#10.1")]
        [TestCase("Version 14.1.555.1234 (…) [PUBLIC] <Releases/14.1>",
            "{x:6-8}_99.9.999.9999_(?)_[{x:6-8}]_<{x:6-8}/99.9>", "14.1")]
        [TestCase("seoul#kr", "aaaaa?aa", "none")]
        [TestCase("KR_1", "aa?9", "1")]
        [TestCase("", "", "none")]
        public void StringValuesUseCategoryFoldingAndLeadingGroups(string raw, string signature, string groups)
        {
            var shape = VersionShapeSignature.Describe("string", raw);
            Assert.That(shape.Type, Is.EqualTo("string"));
            Assert.That(shape.Signature, Is.EqualTo(signature));
            Assert.That(shape.LeadingGroups, Is.EqualTo(groups));
            Assert.That(shape.Truncated, Is.False);
            Assert.That(VersionShapeSignature.IsValidSignature(shape.Signature), Is.True);
            Assert.That(VersionShapeSignature.IsValidLeadingGroups(shape.LeadingGroups), Is.True);
        }

        /// <summary>The hyphenated GUID, hex hash, IPv4 tail and 10-digit value never survive as raw text.</summary>
        [TestCase("a3f9c2d17b4e8a6f0c5d9e2b1a7f4c8d3e6b0a9f")]
        [TestCase("123e4567-e89b-12d3-a456-426614174000")]
        [TestCase("Version 16.18 10.200.30.4")]
        [TestCase("1234567890")]
        [TestCase("Version 14.1.555.1234 (…) [PUBLIC] <Releases/14.1>")]
        [TestCase("버전 십사")]
        public void SentinelValuesDoNotSurviveAnyOutputField(string raw)
        {
            var shape = VersionShapeSignature.Describe("string", raw);
            foreach (var output in new[] { shape.Signature, shape.LeadingGroups })
            {
                Assert.That(output, Does.Not.Contain(raw));
                foreach (var forbidden in new[] { "a3f9c2", "426614174000", "10.200", "200.30", "1234567890", "555", "1234", "PUBLIC", "Releases", "버전", "십사" })
                    Assert.That(output, Does.Not.Contain(forbidden));
            }
        }

        [Test]
        public void VersionPrefixIsRecordedSeparatelyBecauseTheWordFolds()
        {
            Assert.That(VersionShapeSignature.Describe("string", "Version 16.18.1").HasVersionPrefix, Is.True);
            Assert.That(VersionShapeSignature.Describe("string", "16.18.1").HasVersionPrefix, Is.False);
            Assert.That(VersionShapeSignature.Describe("string", "version 16.18.1").HasVersionPrefix, Is.False);
            Assert.That(VersionShapeSignature.Describe("string", "Version").HasVersionPrefix, Is.False);
        }

        [Test]
        public void LongOutputIsTruncatedAtTokenBoundaryWithinSixtyFourCharacters()
        {
            var raw = string.Join(" ", new string('1', 1), "(a)", "(b)", "(c)", "(d)", "(e)", "(f)", "(g)", "(h)",
                "(i)", "(j)", "(k)", "(l)", "(m)", "(n)", "(o)", "(p)", "abcdefghijk");
            var shape = VersionShapeSignature.Describe("string", raw);
            Assert.That(shape.Truncated, Is.True);
            Assert.That(shape.Signature.Length, Is.LessThanOrEqualTo(64));
            Assert.That(VersionShapeSignature.IsValidSignature(shape.Signature), Is.True);
            var folded = VersionShapeSignature.Describe("string", new string('x', 70));
            Assert.That(folded.Signature, Is.EqualTo("{x:65+}"));
            Assert.That(folded.Truncated, Is.False);
        }

        [Test]
        public void JsonTypeIsRecordedFirstForNonStrings()
        {
            var integer = VersionShapeSignature.Describe("integer", "2");
            Assert.That(integer.Type, Is.EqualTo("integer"));
            Assert.That(integer.Signature, Is.EqualTo("9"));
            Assert.That(integer.LeadingGroups, Is.EqualTo("2"));
            foreach (var type in new[] { "null", "missing", "other" })
            {
                var shape = VersionShapeSignature.Describe(type, null);
                Assert.That(shape.Type, Is.EqualTo(type));
                Assert.That(shape.Signature, Is.EqualTo(""));
                Assert.That(shape.LeadingGroups, Is.EqualTo("none"));
                Assert.That(shape.HasVersionPrefix, Is.False);
            }
            Assert.Throws<ArgumentException>(() => VersionShapeSignature.Describe("boolean", "true"));
        }

        /// <summary>The copied regex must stay equivalent to the normal path's private game_version contract.</summary>
        [TestCase("Version 16.18.123.456 (FAKE-BUILD-NOTE)")]
        [TestCase("16.18.123")]
        [TestCase("16.18.123.456")]
        [TestCase("Version 14.1.555.1234 (…) [PUBLIC] <Releases/14.1>")]
        [TestCase("16.18")]
        [TestCase("123456.1.1")]
        [TestCase("16.18.1\nx")]
        [TestCase("version 16.18.1")]
        [TestCase("")]
        public void GameVersionRegexMatchesTheAnonymizerContract(string value)
        {
            var method = typeof(MatchAnonymizer).GetMethod("NumericVersion", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            bool expected;
            string expectedRelease = null;
            try
            {
                var numeric = (string)method.Invoke(null, new object[] { value, MatchPayloadFailure.InvalidPayload });
                expected = true;
                expectedRelease = string.Join(".", numeric.Split('.'), 0, 2);
            }
            catch (TargetInvocationException error) when (error.InnerException is MatchPayloadException) { expected = false; }
            Assert.That(VersionShapeSignature.TryExtractRelease(value, out var release), Is.EqualTo(expected));
            Assert.That(release, Is.EqualTo(expectedRelease));
        }
    }
}
