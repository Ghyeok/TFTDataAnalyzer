using System;
using System.Collections.Generic;
using System.Linq;

namespace TFTAnalyzer.Domain
{
    /// <summary>Ordered source slots. Their positions do not imply star levels.</summary>
    public sealed class StaticNumericSeries
    {
        public string Name { get; }
        public IReadOnlyList<double?> Values { get; }
        public StaticNumericSeries(string name, IEnumerable<double?> values)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Series name is required.");
            Name = name;
            Values = DomainValues.Copy(values);
            foreach (var value in Values) DomainValues.RequireFinite(value);
        }
    }
}
