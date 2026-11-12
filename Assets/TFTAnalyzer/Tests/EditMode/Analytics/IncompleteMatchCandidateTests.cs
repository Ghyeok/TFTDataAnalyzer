using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TFTAnalyzer.Analytics;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Tests.Analytics
{
    public sealed class IncompleteMatchCandidateTests
    {
        private const string Sentinel = "SYNTHETIC_PRIVATE_SENTINEL_NEVER_RETAIN";

        [TestCase(IncompleteCandidateContext.UnsupportedVersion)]
        [TestCase(IncompleteCandidateContext.PatchUnconfirmed)]
        public void UnsupportedOrUnconfirmedContextKeepsEveryNestedCandidateUnverified(IncompleteCandidateContext context)
        {
            var f = Factory(context);
            var match = Match(f);
            var meanings = Reachable(match).OfType<CandidateMeaning>().ToArray();
            Assert.That(meanings.Length, Is.GreaterThan(10));
            Assert.That(meanings.All(x => x == CandidateMeaning.Unverified), Is.True);
            Assert.That(match.Participants.Values[0].Augments.State, Is.EqualTo(CandidateFieldState.EmptyArray));
        }

        [Test]
        public void SupportedContextCanConfirmValuesWithoutTreatingMissingAugmentsAsAnEmptyArray()
        {
            var f = Factory(IncompleteCandidateContext.SupportedVersionPatchConfirmed);
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18),
                f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { Participant(f, f.AugmentsState(CandidateFieldState.Missing)) }));
            Assert.That(match.SetNumber.Meaning, Is.EqualTo(CandidateMeaning.Confirmed));
            Assert.That(match.Participants.Values[0].Placement.Value, Is.EqualTo(1));
            Assert.That(match.Participants.Values[0].Augments.State, Is.EqualTo(CandidateFieldState.Missing));
            Assert.That(match.Participants.Values[0].Augments.Values, Is.Empty);
        }

        [TestCase(CandidateNumberKind.Placement, 1, 8)]
        [TestCase(CandidateNumberKind.LastRound, 0, 100)]
        [TestCase(CandidateNumberKind.SetNumber, 1, 99)]
        [TestCase(CandidateNumberKind.UnitTier, 1, 4)]
        [TestCase(CandidateNumberKind.TraitTier, 0, 10)]
        public void NumericSafetyBoundsAcceptEndpointsAndDiscardOutOfRangeValues(CandidateNumberKind kind, long min, long max)
        {
            var f = Factory();
            Assert.That(f.Number(kind, min).Value, Is.EqualTo((int)min));
            Assert.That(f.Number(kind, max).Value, Is.EqualTo((int)max));
            foreach (var invalid in new[] { min - 1, max + 1, long.MinValue, long.MaxValue })
            {
                var field = f.Number(kind, invalid);
                Assert.That(field.State, Is.EqualTo(CandidateFieldState.OutOfRange));
                Assert.That(field.Value, Is.Null);
                Assert.That(field.HasValue, Is.False);
            }
        }

        [Test]
        public void QueueValueMustMatchTheRequestAndCannotOverflowIntoAMatchingInteger()
        {
            var f = Factory();
            Assert.That(f.Number(CandidateNumberKind.QueueId, 1100).Value, Is.EqualTo(1100));
            foreach (var value in new long[] { 1099, 1101, 0, -1, 4294968396L })
            {
                Assert.That(f.Number(CandidateNumberKind.QueueId, value).State, Is.EqualTo(CandidateFieldState.OutOfRange));
                Assert.That(f.Number(CandidateNumberKind.QueueId, value).Value, Is.Null);
            }
        }

        [TestCase(CandidateFieldState.Missing)]
        [TestCase(CandidateFieldState.Null)]
        [TestCase(CandidateFieldState.InvalidFormat)]
        [TestCase(CandidateFieldState.LimitExceeded)]
        [TestCase(CandidateFieldState.OutOfRange)]
        public void NumericNonvalueStatesNeverRetainANumericValue(CandidateFieldState state)
        {
            var field = Factory().NumberState(CandidateNumberKind.Placement, state);
            Assert.That(field.State, Is.EqualTo(state));
            Assert.That(field.HasValue, Is.False);
            Assert.That(field.Value, Is.Null);
        }

        [Test]
        public void EmptyMissingNullAndInvalidArraysStayDistinct()
        {
            var f = Factory();
            Assert.That(f.Augments(Array.Empty<CandidateIdField>()).State, Is.EqualTo(CandidateFieldState.EmptyArray));
            Assert.That(f.Augments(null).State, Is.EqualTo(CandidateFieldState.Null));
            foreach (var state in new[] { CandidateFieldState.Missing, CandidateFieldState.Null,
                CandidateFieldState.InvalidFormat, CandidateFieldState.LimitExceeded, CandidateFieldState.CatalogUnconfirmed })
            {
                var field = f.AugmentsState(state);
                Assert.That(field.State, Is.EqualTo(state));
                Assert.That(field.Values, Is.Empty);
            }
            Assert.That(f.Augments(new CandidateIdField[] { null }).State, Is.EqualTo(CandidateFieldState.InvalidFormat));
        }

        [Test]
        public void CatalogUnconfirmedArrayMeaningRemainsUnverifiedEvenForConfirmedContext()
        {
            var f = Factory(IncompleteCandidateContext.SupportedVersionPatchConfirmed);
            Assert.That(f.AugmentsState(CandidateFieldState.CatalogUnconfirmed).Meaning, Is.EqualTo(CandidateMeaning.Unverified));
        }

        [TestCase(CandidateIdKind.Unit, "UNIT_A")]
        [TestCase(CandidateIdKind.Item, "ITEM_A")]
        [TestCase(CandidateIdKind.Trait, "TRAIT_A")]
        [TestCase(CandidateIdKind.Augment, "AUGMENT_A")]
        public void ExactIdsAreCopiedFromTheValidatedCatalogAndRemainMeaningUnverified(CandidateIdKind kind, string id)
        {
            var catalog = Catalog();
            var f = new IncompleteCandidateFactory(catalog, IncompleteCandidateContext.UnsupportedVersion, 1100);
            var raw = new string(id.ToCharArray());
            var field = f.Id(kind, raw);
            var expected = kind == CandidateIdKind.Unit ? catalog.Units[0].StableId :
                kind == CandidateIdKind.Item ? catalog.Items[0].StableId :
                kind == CandidateIdKind.Trait ? catalog.Traits[0].StableId : catalog.Augments[0].StableId;
            Assert.That(field.Value, Is.SameAs(expected));
            Assert.That(field.Value, Is.Not.SameAs(raw));
            Assert.That(field.State, Is.EqualTo(CandidateFieldState.Value));
            Assert.That(field.Meaning, Is.EqualTo(CandidateMeaning.Unverified));
        }

        [TestCase(CandidateIdKind.Unit, "ITEM_A")]
        [TestCase(CandidateIdKind.Item, "UNIT_A")]
        [TestCase(CandidateIdKind.Trait, "AUGMENT_A")]
        [TestCase(CandidateIdKind.Augment, "TRAIT_A")]
        [TestCase(CandidateIdKind.Unit, "unit_a")]
        [TestCase(CandidateIdKind.Unit, " UNIT_A ")]
        [TestCase(CandidateIdKind.Unit, Sentinel)]
        public void UnknownWrongKindOrInexactIdsLeaveOnlyCatalogUnconfirmedState(CandidateIdKind kind, string raw)
        {
            var field = Factory(IncompleteCandidateContext.SupportedVersionPatchConfirmed).Id(kind, raw);
            Assert.That(field.State, Is.EqualTo(CandidateFieldState.CatalogUnconfirmed));
            Assert.That(field.Meaning, Is.EqualTo(CandidateMeaning.Unverified));
            Assert.That(field.Value, Is.Null);
            Assert.That(field.HasValue, Is.False);
            Assert.That(Reachable(field).OfType<string>(), Does.Not.Contain(raw));
        }

        [Test]
        public void WithoutAReferenceCatalogNumbersSurviveButIdsAreUnavailable()
        {
            var f = new IncompleteCandidateFactory(null, IncompleteCandidateContext.UnsupportedVersion, 1100);
            Assert.That(f.Number(CandidateNumberKind.Placement, 8).Value, Is.EqualTo(8));
            Assert.That(f.Id(CandidateIdKind.Unit, "UNIT_A").State, Is.EqualTo(CandidateFieldState.CatalogUnconfirmed));
            Assert.That(f.Id(CandidateIdKind.Unit, null).State, Is.EqualTo(CandidateFieldState.Null));
        }

        [Test]
        public void InvalidCatalogIsRejectedWithoutEchoingItsIdsOrValidationDetails()
        {
            var error = Assert.Throws<ArgumentException>(() => new IncompleteCandidateFactory(
                Catalog(true), IncompleteCandidateContext.UnsupportedVersion, 1100));
            Assert.That(error.ToString(), Does.Not.Contain(Sentinel));
            Assert.That(error.InnerException, Is.Null);
        }

        [TestCase("units", 64)]
        [TestCase("traits", 64)]
        [TestCase("augments", 16)]
        [TestCase("items", 16)]
        [TestCase("participants", 8)]
        public void ArraySafetyBoundsPreserveExactLimitAndDiscardEntireOverflow(string kind, int limit)
        {
            var f = Factory();
            object accepted;
            object rejected;
            switch (kind)
            {
                case "units":
                    accepted = f.Units(Enumerable.Repeat(Unit(f), limit));
                    rejected = f.Units(Enumerable.Repeat(Unit(f), limit + 1)); break;
                case "traits":
                    accepted = f.Traits(Enumerable.Repeat(Trait(f), limit));
                    rejected = f.Traits(Enumerable.Repeat(Trait(f), limit + 1)); break;
                case "augments":
                    accepted = f.Augments(Enumerable.Repeat(f.Id(CandidateIdKind.Augment, "AUGMENT_A"), limit));
                    rejected = f.Augments(Enumerable.Repeat(f.Id(CandidateIdKind.Augment, "AUGMENT_A"), limit + 1)); break;
                case "items":
                    accepted = f.Items(Enumerable.Repeat(f.Id(CandidateIdKind.Item, "ITEM_A"), limit));
                    rejected = f.Items(Enumerable.Repeat(f.Id(CandidateIdKind.Item, "ITEM_A"), limit + 1)); break;
                default:
                    accepted = f.Participants(Enumerable.Range(1, limit).Select(x => Participant(f, ordinal: x)));
                    rejected = f.Participants(Enumerable.Repeat(Participant(f), limit + 1)); break;
            }
            Assert.That(Property<CandidateFieldState>(accepted, "State"), Is.EqualTo(CandidateFieldState.Value));
            Assert.That(((IEnumerable)accepted.GetType().GetProperty("Values").GetValue(accepted)).Cast<object>().Count(), Is.EqualTo(limit));
            Assert.That(Property<CandidateFieldState>(rejected, "State"), Is.EqualTo(CandidateFieldState.LimitExceeded));
            Assert.That((IEnumerable)rejected.GetType().GetProperty("Values").GetValue(rejected), Is.Empty);
        }

        [Test]
        public void ArrayEnumerationStopsAtTheFirstExcessElement()
        {
            var f = Factory();
            var result = f.Items(ItemsBeyondBound(f));
            Assert.That(result.State, Is.EqualTo(CandidateFieldState.LimitExceeded));
            Assert.That(result.Values, Is.Empty);
        }

        [Test]
        public void SnapshotCannotBeMutatedThroughInputOrPublicCollections()
        {
            var f = Factory();
            var input = new[] { Unit(f) };
            var units = f.Units(input);
            input[0] = null;
            Assert.That(units.Values[0].StableId.Value, Is.EqualTo("UNIT_A"));
            Assert.Throws<NotSupportedException>(() => ((IList<IncompleteUnitCandidate>)units.Values)[0] = null);
        }

        [Test]
        public void ParticipantOrdinalsAreLocalBoundedAndUnique()
        {
            var f = Factory();
            Assert.Throws<ArgumentOutOfRangeException>(() => Participant(f, ordinal: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Participant(f, ordinal: 9));
            var duplicate = f.Participants(new[] { Participant(f), Participant(f) });
            Assert.That(duplicate.State, Is.EqualTo(CandidateFieldState.InvalidFormat));
            Assert.That(duplicate.Values, Is.Empty);
        }

        [Test]
        public void FactoryRejectsCrossKindNumericIdsAndForeignContextFields()
        {
            var f = Factory();
            var other = Factory(IncompleteCandidateContext.SupportedVersionPatchConfirmed);
            Assert.Throws<ArgumentException>(() => f.Unit(f.Id(CandidateIdKind.Item, "ITEM_A"),
                f.Number(CandidateNumberKind.UnitTier, 1), f.Items(Array.Empty<CandidateIdField>())));
            Assert.Throws<ArgumentException>(() => f.Unit(f.Id(CandidateIdKind.Unit, "UNIT_A"),
                f.Number(CandidateNumberKind.Placement, 1), f.Items(Array.Empty<CandidateIdField>())));
            Assert.Throws<ArgumentException>(() => f.Unit(other.Id(CandidateIdKind.Unit, "UNIT_A"),
                f.Number(CandidateNumberKind.UnitTier, 1), f.Items(Array.Empty<CandidateIdField>())));
            Assert.Throws<ArgumentException>(() => f.Items(new[] { f.Id(CandidateIdKind.Augment, "AUGMENT_A") }));
            Assert.Throws<ArgumentException>(() => f.Units(new[] { Unit(other) }));
        }

        [Test]
        public void InvalidEnumsAndForgedValueStatesCannotCreateCandidates()
        {
            var f = Factory();
            Assert.Throws<ArgumentOutOfRangeException>(() => new IncompleteCandidateFactory(
                Catalog(), (IncompleteCandidateContext)99, 1100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IncompleteCandidateFactory(
                Catalog(), IncompleteCandidateContext.UnsupportedVersion, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Number((CandidateNumberKind)99, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Id((CandidateIdKind)99, Sentinel));
            Assert.Throws<ArgumentException>(() => f.NumberState(CandidateNumberKind.Placement, CandidateFieldState.Value));
            Assert.Throws<ArgumentException>(() => f.NumberState(CandidateNumberKind.Placement, CandidateFieldState.EmptyArray));
            Assert.Throws<ArgumentException>(() => f.IdState(CandidateIdKind.Unit, CandidateFieldState.Value));
            Assert.Throws<ArgumentException>(() => f.UnitsState(CandidateFieldState.Value));
            Assert.Throws<ArgumentException>(() => f.UnitsState((CandidateFieldState)99));
        }

        [Test]
        public void CandidateGraphContainsNoRawIdsOrCatalogMetadataAndHasNoPublicConstructors()
        {
            var f = Factory();
            var unit = f.Unit(f.Id(CandidateIdKind.Unit, Sentinel),
                f.Number(CandidateNumberKind.UnitTier, 4), f.Items(new[] { f.Id(CandidateIdKind.Item, Sentinel) }));
            var participant = f.Participant(1, f.Number(CandidateNumberKind.Placement, 1),
                f.NumberState(CandidateNumberKind.LastRound, CandidateFieldState.InvalidFormat),
                f.Units(new[] { unit }), f.Traits(new[] { f.Trait(f.Id(CandidateIdKind.Trait, Sentinel),
                    f.Number(CandidateNumberKind.TraitTier, 0)) }),
                f.Augments(new[] { f.Id(CandidateIdKind.Augment, Sentinel) }));
            var match = f.Match(f.Number(CandidateNumberKind.SetNumber, 18),
                f.Number(CandidateNumberKind.QueueId, 1100), f.Participants(new[] { participant }));
            Assert.That(Reachable(match).OfType<string>().Any(x => x.Contains(Sentinel)), Is.False);
            Assert.That(JsonConvert.SerializeObject(match), Does.Not.Contain(Sentinel));
            foreach (var type in new[] { typeof(CandidateNumberField), typeof(CandidateIdField),
                typeof(CandidateList<IncompleteUnitCandidate>), typeof(IncompleteUnitCandidate),
                typeof(IncompleteTraitCandidate), typeof(IncompleteParticipantCandidate), typeof(IncompleteMatchCandidate) })
                Assert.That(type.GetConstructors(), Is.Empty, type.Name);
            Assert.That(typeof(AnonymousMatchObservation).IsAssignableFrom(match.GetType()), Is.False);
        }

        private static IEnumerable<CandidateIdField> ItemsBeyondBound(IncompleteCandidateFactory f)
        {
            for (var i = 0; i < 17; i++) yield return f.Id(CandidateIdKind.Item, "ITEM_A");
            throw new InvalidOperationException("Collection was read beyond its safety bound.");
        }

        private static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name).GetValue(value);

        private static IEnumerable<object> Reachable(object root)
        {
            var seen = new HashSet<object>();
            var pending = new Stack<object>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var value = pending.Pop();
                if (value == null) continue;
                var type = value.GetType();
                if (type.IsEnum || type.IsPrimitive || value is string) { yield return value; continue; }
                if (!seen.Add(value)) continue;
                yield return value;
                if (value is IEnumerable enumerable)
                {
                    foreach (var child in enumerable) pending.Push(child);
                    continue;
                }
                for (var current = type; current != null; current = current.BaseType)
                    foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        pending.Push(field.GetValue(value));
            }
        }

        private static IncompleteCandidateFactory Factory(IncompleteCandidateContext context = IncompleteCandidateContext.UnsupportedVersion) =>
            new IncompleteCandidateFactory(Catalog(), context, 1100);

        private static IncompleteUnitCandidate Unit(IncompleteCandidateFactory f) =>
            f.Unit(f.Id(CandidateIdKind.Unit, "UNIT_A"), f.Number(CandidateNumberKind.UnitTier, 2),
                f.Items(new[] { f.Id(CandidateIdKind.Item, "ITEM_A") }));

        private static IncompleteTraitCandidate Trait(IncompleteCandidateFactory f) =>
            f.Trait(f.Id(CandidateIdKind.Trait, "TRAIT_A"), f.Number(CandidateNumberKind.TraitTier, 1));

        private static IncompleteParticipantCandidate Participant(IncompleteCandidateFactory f,
            CandidateList<CandidateIdField> augments = null, int ordinal = 1) =>
            f.Participant(ordinal, f.Number(CandidateNumberKind.Placement, 1), f.Number(CandidateNumberKind.LastRound, 30),
                f.Units(new[] { Unit(f) }), f.Traits(new[] { Trait(f) }),
                augments ?? f.Augments(Array.Empty<CandidateIdField>()));

        private static IncompleteMatchCandidate Match(IncompleteCandidateFactory f) =>
            f.Match(f.Number(CandidateNumberKind.SetNumber, 18), f.Number(CandidateNumberKind.QueueId, 1100),
                f.Participants(new[] { Participant(f) }));

        private static StaticCatalog Catalog(bool invalid = false)
        {
            var name = new LocalizedText("", Sentinel);
            var manifest = new PatchManifest(1, new PatchKey("TEST_SET", 18, "18.2", "16.18.1"), Sentinel,
                DateTime.UnixEpoch, "", null, DateTime.UnixEpoch, null, 0, 0, Sentinel, "");
            return new StaticCatalog(manifest,
                new[] { new UnitRecord(invalid ? Sentinel : "UNIT_A", name, invalid ? 0 : 1, null, "", null) },
                new[] { new TraitRecord("TRAIT_A", name, null, null, "", null) },
                new[] { new ItemRecord("ITEM_A", name, null, null, null, "", null) },
                new[] { new AugmentRecord("AUGMENT_A", name, "", null, null, null, "", null) });
        }
    }
}
