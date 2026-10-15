using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Serialization;

namespace TFTAnalyzer.Analytics
{
    public static class CompositionFingerprintBuilder
    {
        public static CompositionFingerprint Build(AnonymousParticipant participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            var units = participant.Units.Select(unit => new
                {
                    Unit = unit,
                    Items = new JArray(unit.ItemIds.OrderBy(id => id, StringComparer.Ordinal))
                })
                .OrderBy(value => value.Unit.StableId, StringComparer.Ordinal)
                .ThenBy(value => value.Unit.Tier)
                .ThenBy(value => value.Items.ToString(Formatting.None), StringComparer.Ordinal)
                .Select(value => new JArray(value.Unit.StableId, value.Unit.Tier, value.Items));
            var traits = participant.Traits.Where(trait => trait.IsActive)
                .Select(trait => trait.StableId).OrderBy(id => id, StringComparer.Ordinal);
            var augments = participant.AugmentIds.OrderBy(id => id, StringComparer.Ordinal);
            // Arrays provide unambiguous boundaries even when stable IDs contain delimiters.
            var canonical = new JArray(new JArray(units), new JArray(traits), new JArray(augments));
            return new CompositionFingerprint(ContentHasher.Sha256Hex(
                "fingerprint-v1|" + canonical.ToString(Formatting.None)));
        }
    }
}
