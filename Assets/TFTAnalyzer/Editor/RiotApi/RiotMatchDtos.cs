using System;
using Newtonsoft.Json;
using TFTAnalyzer.Analytics;

namespace TFTAnalyzer.Editor.RiotApi
{
    // Private-to-assembly allowlist: personal fields are skipped, never stored in DTOs.
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchDto
    {
        [JsonProperty("metadata", Required = Required.Always)] public RiotMatchMetadata Metadata;
        [JsonProperty("info", Required = Required.Always)] public RiotMatchInfo Info;

        /// <summary>Releases raw match/version and all DTO collection references on every exit.</summary>
        internal void Clear()
        {
            if (Metadata != null) { Metadata.MatchId = null; Metadata.DataVersion = null; }
            if (Info != null)
            {
                Info.GameVersion = null;
                if (Info.Participants != null)
                    foreach (var participant in Info.Participants)
                    {
                        if (participant == null) continue;
                        if (participant.Units != null)
                            foreach (var unit in participant.Units)
                            {
                                if (unit == null) continue;
                                unit.CharacterId = null;
                                if (unit.ItemNames != null) Array.Clear(unit.ItemNames, 0, unit.ItemNames.Length);
                            }
                        if (participant.Traits != null)
                            foreach (var trait in participant.Traits) if (trait != null) trait.Name = null;
                        if (participant.Augments != null) Array.Clear(participant.Augments, 0, participant.Augments.Length);
                        participant.Units = null; participant.Traits = null; participant.Augments = null;
                    }
                Info.Participants = null;
            }
            Metadata = null; Info = null;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchMetadata
    {
        [JsonProperty("data_version", Required = Required.Always)] public string DataVersion;
        [JsonProperty("match_id", Required = Required.Always)] public string MatchId;
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchInfo
    {
        [JsonProperty("game_version", Required = Required.Always)] public string GameVersion;
        [JsonProperty("game_datetime", Required = Required.Always)] public long? GameDatetime;
        [JsonProperty("queue_id", Required = Required.Always)] public int? QueueId;
        [JsonProperty("tft_set_number", Required = Required.Always)] public int? SetNumber;
        [JsonProperty("participants", Required = Required.Always)] public RiotMatchParticipant[] Participants;
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchParticipant
    {
        [JsonProperty("placement", Required = Required.Always)] public int? Placement;
        [JsonProperty("last_round", Required = Required.Always)] public int? LastRound;
        [JsonProperty("units", Required = Required.Always)] public RiotMatchUnit[] Units;
        [JsonProperty("traits", Required = Required.Always)] public RiotMatchTrait[] Traits;
        private string[] _augments;
        internal bool HasAugments;
        [JsonProperty("augments"), JsonConverter(typeof(AugmentsConverter))]
        public string[] Augments
        {
            get => _augments;
            set { HasAugments = true; _augments = value; }
        }
    }

    /// <summary>Diagnoses only the field envelope; untrusted array elements retain general validation.</summary>
    internal sealed class AugmentsConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type type) => type == typeof(string[]);
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) throw new MatchPayloadException(MatchPayloadFailure.AugmentsNull);
            if (reader.TokenType != JsonToken.StartArray) throw new MatchPayloadException(MatchPayloadFailure.AugmentsInvalidFormat);
            return serializer.Deserialize<string[]>(reader);
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchUnit
    {
        [JsonProperty("character_id", Required = Required.Always)] public string CharacterId;
        [JsonProperty("tier", Required = Required.Always)] public int? Tier;
        [JsonProperty("itemNames", Required = Required.Always)] public string[] ItemNames;
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class RiotMatchTrait
    {
        [JsonProperty("name", Required = Required.Always)] public string Name;
        [JsonProperty("tier_current", Required = Required.Always)] public int? TierCurrent;
    }
}
