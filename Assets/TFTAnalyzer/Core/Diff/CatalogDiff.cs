using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Diff
{
    public sealed class FieldChange
    {
        public string FieldName { get; }
        public string Before { get; }
        public string After { get; }

        public FieldChange(string fieldName, string before, string after)
        {
            FieldName = fieldName ?? "";
            Before = before ?? "";
            After = after ?? "";
        }
    }

    public sealed class RecordChange
    {
        public string StableId { get; }
        public IReadOnlyList<FieldChange> Fields { get; }

        public RecordChange(string stableId, IEnumerable<FieldChange> fields)
        {
            StableId = stableId ?? "";
            Fields = Array.AsReadOnly((fields ?? Enumerable.Empty<FieldChange>()).ToArray());
        }
    }

    public sealed class EntityDiff
    {
        public IReadOnlyList<string> AddedIds { get; }
        public IReadOnlyList<string> RemovedIds { get; }
        public IReadOnlyList<RecordChange> Changed { get; }

        public EntityDiff(
            IEnumerable<string> addedIds,
            IEnumerable<string> removedIds,
            IEnumerable<RecordChange> changed)
        {
            AddedIds = Stable(addedIds);
            RemovedIds = Stable(removedIds);
            Changed = Array.AsReadOnly((changed ?? Enumerable.Empty<RecordChange>())
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray());
        }

        private static IReadOnlyList<string> Stable(IEnumerable<string> values)
        {
            return Array.AsReadOnly((values ?? Enumerable.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        }
    }

    public sealed class CatalogDiff
    {
        public EntityDiff Units { get; }
        public EntityDiff Traits { get; }
        public EntityDiff Items { get; }
        public EntityDiff Augments { get; }

        public CatalogDiff(
            EntityDiff units,
            EntityDiff traits,
            EntityDiff items,
            EntityDiff augments)
        {
            Units = units;
            Traits = traits;
            Items = items;
            Augments = augments;
        }
    }
}
