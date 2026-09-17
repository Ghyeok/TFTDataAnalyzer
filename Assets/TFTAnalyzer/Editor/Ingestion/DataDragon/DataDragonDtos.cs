using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion.DataDragon
{
    public enum DataDragonDocumentKind
    {
        Champions,
        Traits,
        Items,
        Augments
    }

    public sealed class DataDragonRequest
    {
        public PatchKey Patch { get; }

        public DataDragonRequest(PatchKey patch)
        {
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        }
    }

    public sealed class DataDragonPayloadPair
    {
        public HttpResponseData KoKr { get; }
        public HttpResponseData EnUs { get; }

        public DataDragonPayloadPair(HttpResponseData koKr, HttpResponseData enUs)
        {
            KoKr = koKr ?? throw new ArgumentNullException(nameof(koKr));
            EnUs = enUs ?? throw new ArgumentNullException(nameof(enUs));
        }
    }

    public sealed class DataDragonSnapshot
    {
        public DataDragonPayloadPair Champions { get; }
        public DataDragonPayloadPair Traits { get; }
        public DataDragonPayloadPair Items { get; }
        public DataDragonPayloadPair Augments { get; }

        public IReadOnlyList<HttpResponseData> AllResponses => new[]
        {
            Champions.KoKr, Champions.EnUs,
            Traits.KoKr, Traits.EnUs,
            Items.KoKr, Items.EnUs,
            Augments.KoKr, Augments.EnUs
        };

        public DataDragonSnapshot(
            DataDragonPayloadPair champions,
            DataDragonPayloadPair traits,
            DataDragonPayloadPair items,
            DataDragonPayloadPair augments)
        {
            Champions = champions ?? throw new ArgumentNullException(nameof(champions));
            Traits = traits ?? throw new ArgumentNullException(nameof(traits));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Augments = augments ?? throw new ArgumentNullException(nameof(augments));
        }
    }
}
