using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TFTAnalyzer.Domain;

namespace TFTAnalyzer.Editor.Ingestion
{
    // Called at the parser's actual joined record, before the selected document identity is lost.
    internal static class ParsedFieldOrigins
    {
        public static void Add(List<FieldOrigin> origins, string type, object value,
            JObject ko, JObject en, SourceStamp koSource, SourceStamp enSource)
        {
            var primary = en ?? ko;
            var primarySource = en != null ? enSource : koSource;
            var id = value is UnitRecord unit ? unit.StableId : value is TraitRecord trait ? trait.StableId :
                value is ItemRecord item ? item.StableId : ((AugmentRecord)value).StableId;
            foreach (var pointer in FieldOriginFields.Required(value))
            {
                // A default int is not evidence that a source supplied a cost. A real zero is evidence.
                if (pointer == "/cost" && !new[] { "cost", "tier" }.Any(name => primary?[name]?.Type == JTokenType.Integer))
                    continue;
                var source = pointer == "/name/koKr" || pointer == "/description/koKr" ? koSource :
                    pointer == "/name/enUs" || pointer == "/description/enUs" ? enSource : primarySource;
                origins.Add(new FieldOrigin(type, id, pointer, new[] { source }));
            }
        }
    }
}
