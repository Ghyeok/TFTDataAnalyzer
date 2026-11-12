using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Validation;

namespace TFTAnalyzer.Analytics
{
    /// <summary>Structural receipt state, separate from the meaning of any received value.</summary>
    public enum CandidateFieldState
    {
        Value, EmptyArray, Missing, Null, InvalidFormat, LimitExceeded, CatalogUnconfirmed, OutOfRange
    }

    public enum CandidateMeaning { Confirmed, Unverified }
    public enum CandidateNumberKind { Placement, LastRound, SetNumber, QueueId, UnitTier, TraitTier }
    public enum CandidateIdKind { Unit, Item, Trait, Augment }

    /// <summary>The caller supplies only its validated version/patch decision, never a raw version.</summary>
    public enum IncompleteCandidateContext
    {
        SupportedVersionPatchConfirmed, UnsupportedVersion, PatchUnconfirmed
    }

    /// <summary>Factory ownership prevents mixing catalogs or confirmation contexts within a graph.</summary>
    public abstract class IncompleteCandidateNode
    {
        internal object Owner { get; }
        public CandidateMeaning Meaning { get; }
        internal IncompleteCandidateNode(object owner, CandidateMeaning meaning)
        {
            Owner = owner;
            Meaning = meaning;
        }
    }

    public sealed class CandidateNumberField : IncompleteCandidateNode
    {
        public CandidateNumberKind Kind { get; }
        public CandidateFieldState State { get; }
        public bool HasValue => State == CandidateFieldState.Value;
        public int? Value { get; }
        internal CandidateNumberField(object owner, CandidateMeaning meaning, CandidateNumberKind kind,
            CandidateFieldState state, int? value = null) : base(owner, meaning)
        { Kind = kind; State = state; Value = value; }
    }

    public sealed class CandidateIdField : IncompleteCandidateNode
    {
        public CandidateIdKind Kind { get; }
        public CandidateFieldState State { get; }
        public bool HasValue => State == CandidateFieldState.Value;
        /// <summary>Only a matching ID obtained from the reference catalog; otherwise null.</summary>
        public string Value { get; }
        internal CandidateIdField(object owner, CandidateMeaning meaning, CandidateIdKind kind,
            CandidateFieldState state, string value = null) : base(owner, meaning)
        { Kind = kind; State = state; Value = value; }
    }

    public sealed class CandidateList<T> : IncompleteCandidateNode
    {
        internal CandidateArrayKind Kind { get; }
        public CandidateFieldState State { get; }
        public IReadOnlyList<T> Values { get; }
        internal CandidateList(object owner, CandidateMeaning meaning, CandidateArrayKind kind,
            CandidateFieldState state, IEnumerable<T> values = null) : base(owner, meaning)
        {
            Kind = kind;
            State = state;
            Values = Array.AsReadOnly((values ?? Enumerable.Empty<T>()).ToArray());
        }
    }

    public sealed class IncompleteUnitCandidate : IncompleteCandidateNode
    {
        public CandidateIdField StableId { get; }
        public CandidateNumberField Tier { get; }
        public CandidateList<CandidateIdField> Items { get; }
        internal IncompleteUnitCandidate(object owner, CandidateMeaning meaning, CandidateIdField id,
            CandidateNumberField tier, CandidateList<CandidateIdField> items) : base(owner, meaning)
        { StableId = id; Tier = tier; Items = items; }
    }

    public sealed class IncompleteTraitCandidate : IncompleteCandidateNode
    {
        public CandidateIdField StableId { get; }
        public CandidateNumberField Tier { get; }
        internal IncompleteTraitCandidate(object owner, CandidateMeaning meaning, CandidateIdField id,
            CandidateNumberField tier) : base(owner, meaning)
        { StableId = id; Tier = tier; }
    }

    public sealed class IncompleteParticipantCandidate : IncompleteCandidateNode
    {
        /// <summary>A local position from 1 to 8, never an account identifier.</summary>
        public int LocalOrdinal { get; }
        public CandidateNumberField Placement { get; }
        public CandidateNumberField LastRound { get; }
        public CandidateList<IncompleteUnitCandidate> Units { get; }
        public CandidateList<IncompleteTraitCandidate> Traits { get; }
        public CandidateList<CandidateIdField> Augments { get; }
        internal IncompleteParticipantCandidate(object owner, CandidateMeaning meaning, int ordinal,
            CandidateNumberField placement, CandidateNumberField lastRound,
            CandidateList<IncompleteUnitCandidate> units, CandidateList<IncompleteTraitCandidate> traits,
            CandidateList<CandidateIdField> augments) : base(owner, meaning)
        {
            LocalOrdinal = ordinal; Placement = placement; LastRound = lastRound;
            Units = units; Traits = traits; Augments = augments;
        }
    }

    /// <summary>Separate incomplete receipt model with no match identity, raw metadata or statistics scope.</summary>
    public sealed class IncompleteMatchCandidate : IncompleteCandidateNode
    {
        public CandidateNumberField SetNumber { get; }
        public CandidateNumberField QueueId { get; }
        public CandidateList<IncompleteParticipantCandidate> Participants { get; }
        internal IncompleteMatchCandidate(object owner, CandidateMeaning meaning, CandidateNumberField setNumber,
            CandidateNumberField queueId, CandidateList<IncompleteParticipantCandidate> participants)
            : base(owner, meaning)
        { SetNumber = setNumber; QueueId = queueId; Participants = participants; }
    }

    internal enum CandidateArrayKind { Units, Traits, Items, Augments, Participants }

    /// <summary>
    /// Builds bounded immutable candidates. Raw ID strings are used only for exact lookup and never retained.
    /// The supplied catalog must come from the user's selected reference catalog; structural validation here
    /// does not establish that this catalog describes the received match's actual patch.
    /// </summary>
    public sealed class IncompleteCandidateFactory
    {
        private readonly object _owner = new object();
        private readonly CandidateMeaning _meaning;
        private readonly int _requestedQueueId;
        private readonly Dictionary<CandidateIdKind, Dictionary<string, string>> _ids;

        public IncompleteCandidateFactory(StaticCatalog referenceCatalog, IncompleteCandidateContext context,
            int requestedQueueId)
        {
            if (!Enum.IsDefined(typeof(IncompleteCandidateContext), context))
                throw new ArgumentOutOfRangeException(nameof(context));
            if (requestedQueueId <= 0) throw new ArgumentOutOfRangeException(nameof(requestedQueueId));
            _meaning = context == IncompleteCandidateContext.SupportedVersionPatchConfirmed
                ? CandidateMeaning.Confirmed : CandidateMeaning.Unverified;
            _requestedQueueId = requestedQueueId;
            _ids = new Dictionary<CandidateIdKind, Dictionary<string, string>>();
            if (referenceCatalog == null) return;
            bool valid;
            try { valid = new CatalogValidator().Validate(referenceCatalog).CanPublish; }
            catch (Exception)
            {
                // Validator details can contain source strings. Never propagate them across this boundary.
                throw new ArgumentException("Reference catalog validation failed.");
            }
            if (!valid) throw new ArgumentException("Reference catalog validation failed.");
            _ids.Add(CandidateIdKind.Unit, Ids(referenceCatalog.Units.Select(x => x.StableId)));
            _ids.Add(CandidateIdKind.Item, Ids(referenceCatalog.Items.Select(x => x.StableId)));
            _ids.Add(CandidateIdKind.Trait, Ids(referenceCatalog.Traits.Select(x => x.StableId)));
            _ids.Add(CandidateIdKind.Augment, Ids(referenceCatalog.Augments.Select(x => x.StableId)));
        }

        public CandidateNumberField Number(CandidateNumberKind kind, long value)
        {
            RequireEnum(kind);
            long min, max;
            switch (kind)
            {
                case CandidateNumberKind.Placement: min = 1; max = 8; break;
                case CandidateNumberKind.LastRound: min = 0; max = 100; break;
                case CandidateNumberKind.SetNumber: min = 1; max = 99; break;
                case CandidateNumberKind.UnitTier: min = 1; max = 4; break;
                case CandidateNumberKind.TraitTier: min = 0; max = 10; break;
                default: min = _requestedQueueId; max = _requestedQueueId; break;
            }
            return value < min || value > max
                ? NumberState(kind, CandidateFieldState.OutOfRange)
                : new CandidateNumberField(_owner, _meaning, kind, CandidateFieldState.Value, (int)value);
        }

        public CandidateNumberField NumberState(CandidateNumberKind kind, CandidateFieldState state)
        {
            RequireEnum(kind); RequireNonvalueState(state);
            if (state == CandidateFieldState.EmptyArray || state == CandidateFieldState.CatalogUnconfirmed)
                throw new ArgumentException("Invalid numeric field state.");
            return new CandidateNumberField(_owner, _meaning, kind, state);
        }

        public CandidateIdField Id(CandidateIdKind kind, string transientId)
        {
            RequireEnum(kind);
            if (transientId == null) return IdState(kind, CandidateFieldState.Null);
            if (_ids.TryGetValue(kind, out var ids) && ids.TryGetValue(transientId, out var catalogId))
                return new CandidateIdField(_owner, _meaning, kind, CandidateFieldState.Value, catalogId);
            return IdState(kind, CandidateFieldState.CatalogUnconfirmed);
        }

        public CandidateIdField IdState(CandidateIdKind kind, CandidateFieldState state)
        {
            RequireEnum(kind); RequireNonvalueState(state);
            if (state == CandidateFieldState.EmptyArray || state == CandidateFieldState.OutOfRange)
                throw new ArgumentException("Invalid catalog ID field state.");
            return new CandidateIdField(_owner,
                state == CandidateFieldState.CatalogUnconfirmed ? CandidateMeaning.Unverified : _meaning, kind, state);
        }

        public CandidateList<IncompleteUnitCandidate> Units(IEnumerable<IncompleteUnitCandidate> values) =>
            List(values, CandidateArrayKind.Units, 64);
        public CandidateList<IncompleteTraitCandidate> Traits(IEnumerable<IncompleteTraitCandidate> values) =>
            List(values, CandidateArrayKind.Traits, 64);
        public CandidateList<CandidateIdField> Items(IEnumerable<CandidateIdField> values) =>
            List(values, CandidateArrayKind.Items, 16, x => RequireId(x, CandidateIdKind.Item));
        public CandidateList<CandidateIdField> Augments(IEnumerable<CandidateIdField> values) =>
            List(values, CandidateArrayKind.Augments, 16, x => RequireId(x, CandidateIdKind.Augment));
        public CandidateList<IncompleteParticipantCandidate> Participants(IEnumerable<IncompleteParticipantCandidate> values)
        {
            var result = List(values, CandidateArrayKind.Participants, 8);
            return result.Values.Select(x => x.LocalOrdinal).Distinct().Count() != result.Values.Count
                ? ParticipantsState(CandidateFieldState.InvalidFormat) : result;
        }

        public CandidateList<IncompleteUnitCandidate> UnitsState(CandidateFieldState state) =>
            ListState<IncompleteUnitCandidate>(CandidateArrayKind.Units, state);
        public CandidateList<IncompleteTraitCandidate> TraitsState(CandidateFieldState state) =>
            ListState<IncompleteTraitCandidate>(CandidateArrayKind.Traits, state);
        public CandidateList<CandidateIdField> ItemsState(CandidateFieldState state) =>
            ListState<CandidateIdField>(CandidateArrayKind.Items, state);
        public CandidateList<CandidateIdField> AugmentsState(CandidateFieldState state) =>
            ListState<CandidateIdField>(CandidateArrayKind.Augments, state);
        public CandidateList<IncompleteParticipantCandidate> ParticipantsState(CandidateFieldState state) =>
            ListState<IncompleteParticipantCandidate>(CandidateArrayKind.Participants, state);

        public IncompleteUnitCandidate Unit(CandidateIdField id, CandidateNumberField tier,
            CandidateList<CandidateIdField> items)
        {
            RequireId(id, CandidateIdKind.Unit); RequireNumber(tier, CandidateNumberKind.UnitTier);
            RequireList(items, CandidateArrayKind.Items);
            return new IncompleteUnitCandidate(_owner, _meaning, id, tier, items);
        }

        public IncompleteTraitCandidate Trait(CandidateIdField id, CandidateNumberField tier)
        {
            RequireId(id, CandidateIdKind.Trait); RequireNumber(tier, CandidateNumberKind.TraitTier);
            return new IncompleteTraitCandidate(_owner, _meaning, id, tier);
        }

        public IncompleteParticipantCandidate Participant(int localOrdinal, CandidateNumberField placement,
            CandidateNumberField lastRound, CandidateList<IncompleteUnitCandidate> units,
            CandidateList<IncompleteTraitCandidate> traits, CandidateList<CandidateIdField> augments)
        {
            if (localOrdinal < 1 || localOrdinal > 8) throw new ArgumentOutOfRangeException(nameof(localOrdinal));
            RequireNumber(placement, CandidateNumberKind.Placement); RequireNumber(lastRound, CandidateNumberKind.LastRound);
            RequireList(units, CandidateArrayKind.Units); RequireList(traits, CandidateArrayKind.Traits);
            RequireList(augments, CandidateArrayKind.Augments);
            return new IncompleteParticipantCandidate(_owner, _meaning, localOrdinal, placement, lastRound, units, traits, augments);
        }

        public IncompleteMatchCandidate Match(CandidateNumberField setNumber, CandidateNumberField queueId,
            CandidateList<IncompleteParticipantCandidate> participants)
        {
            RequireNumber(setNumber, CandidateNumberKind.SetNumber); RequireNumber(queueId, CandidateNumberKind.QueueId);
            RequireList(participants, CandidateArrayKind.Participants);
            return new IncompleteMatchCandidate(_owner, _meaning, setNumber, queueId, participants);
        }

        private static Dictionary<string, string> Ids(IEnumerable<string> values) =>
            values.ToDictionary(x => x, x => x, StringComparer.Ordinal);

        private CandidateList<T> List<T>(IEnumerable<T> values, CandidateArrayKind kind, int max,
            Action<T> validate = null) where T : IncompleteCandidateNode
        {
            if (values == null) return ListState<T>(kind, CandidateFieldState.Null);
            var copy = new List<T>();
            bool overflow;
            try
            {
                overflow = false;
                foreach (var value in values)
                {
                    if (copy.Count == max) { overflow = true; break; }
                    copy.Add(value);
                }
            }
            catch (Exception)
            {
                throw new ArgumentException("Candidate collection could not be read.");
            }
            if (overflow) return ListState<T>(kind, CandidateFieldState.LimitExceeded);
            if (copy.Any(x => x == null)) return ListState<T>(kind, CandidateFieldState.InvalidFormat);
            foreach (var value in copy) { RequireOwner(value); validate?.Invoke(value); }
            return copy.Count == 0 ? ListState<T>(kind, CandidateFieldState.EmptyArray)
                : new CandidateList<T>(_owner, _meaning, kind, CandidateFieldState.Value, copy);
        }

        private CandidateList<T> ListState<T>(CandidateArrayKind kind, CandidateFieldState state)
        {
            RequireNonvalueState(state);
            if (state == CandidateFieldState.OutOfRange) throw new ArgumentException("Invalid array field state.");
            return new CandidateList<T>(_owner, _meaning, kind, state);
        }

        private void RequireOwner(IncompleteCandidateNode value)
        {
            if (value == null || !ReferenceEquals(value.Owner, _owner))
                throw new ArgumentException("Candidate belongs to a different factory or is absent.");
        }

        private void RequireNumber(CandidateNumberField value, CandidateNumberKind kind)
        {
            RequireOwner(value);
            if (value.Kind != kind) throw new ArgumentException("Numeric field kind does not match.");
        }

        private void RequireId(CandidateIdField value, CandidateIdKind kind)
        {
            RequireOwner(value);
            if (value.Kind != kind) throw new ArgumentException("Catalog ID kind does not match.");
        }

        private void RequireList<T>(CandidateList<T> value, CandidateArrayKind kind)
        {
            RequireOwner(value);
            if (value.Kind != kind) throw new ArgumentException("Array field kind does not match.");
        }

        private static void RequireEnum<T>(T value) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static void RequireNonvalueState(CandidateFieldState state)
        {
            if (!Enum.IsDefined(typeof(CandidateFieldState), state) || state == CandidateFieldState.Value)
                throw new ArgumentException("An explicit nonvalue state is required.");
        }
    }
}
