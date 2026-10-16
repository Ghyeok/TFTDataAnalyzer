using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Analytics
{
    /// <summary>Separates approved values, insufficient samples and policy exclusions before presentation.</summary>
    public enum MetricVisibility { Visible, InsufficientSample, PolicyBlocked }

    /// <summary>One approved ratio; never constructed for suppressed results.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PresentedRatio
    {
        [JsonProperty] public string Key { get; }
        [JsonProperty] public int Numerator { get; }
        [JsonProperty] public int Denominator { get; }
        [JsonProperty] public double Rate { get; }
        [JsonProperty] public ConfidenceInterval Interval { get; }

        /// <summary>Derives a visible historical ratio and default Wilson interval from approved raw counts.</summary>
        internal PresentedRatio(string key, int numerator, int denominator)
        {
            Key = key;
            Numerator = numerator;
            Denominator = denominator;
            Rate = (double)numerator / denominator;
            Interval = WilsonInterval.Calculate(numerator, denominator);
        }
    }

    /// <summary>An allowlisted presentation payload with no reference to raw observations, metadata or aggregates.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MetricPresentation
    {
        [JsonProperty] public MetricVisibility State { get; }
        [JsonProperty] public string ReasonCode { get; }
        [JsonProperty] public string CopyKey { get; }
        [JsonProperty] public MetricScope Scope { get; }
        [JsonProperty] public int SampleCount { get; }
        [JsonProperty] public double? AveragePlacement { get; }
        [JsonProperty] public string FirstFingerprint { get; }
        [JsonProperty] public string SecondFingerprint { get; }
        [JsonProperty] public int AmbiguousLobbyCount { get; }
        [JsonProperty] public DateTimeOffset? FirstPlayedAt { get; }
        [JsonProperty] public DateTimeOffset? LastPlayedAt { get; }
        [JsonProperty] public IReadOnlyList<PresentedRatio> Ratios { get; }

        /// <summary>Copies only approved fields and an empty ratio list for every suppressed result.</summary>
        internal MetricPresentation(MetricVisibility state, string reasonCode, string copyKey, MetricScope scope,
            int sampleCount, double? averagePlacement, string firstFingerprint, string secondFingerprint,
            int ambiguousLobbyCount, DateTimeOffset? firstPlayedAt, DateTimeOffset? lastPlayedAt,
            IEnumerable<PresentedRatio> ratios)
        {
            State = state;
            ReasonCode = reasonCode;
            CopyKey = copyKey;
            Scope = scope;
            SampleCount = sampleCount;
            AveragePlacement = averagePlacement;
            FirstFingerprint = firstFingerprint;
            SecondFingerprint = secondFingerprint;
            AmbiguousLobbyCount = ambiguousLobbyCount;
            FirstPlayedAt = firstPlayedAt;
            LastPlayedAt = lastPlayedAt;
            Ratios = DomainValues.Copy(ratios);
        }

        /// <summary>Serializes approved fields using isolated settings instead of caller-controlled global defaults.</summary>
        public string ToJson()
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture, TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None,
                DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", DateTimeZoneHandling = DateTimeZoneHandling.Utc
            });
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text))
            {
                serializer.Serialize(writer, this);
                return text.ToString();
            }
        }
    }
}
