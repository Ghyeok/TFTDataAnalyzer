using System;

namespace TFTAnalyzer.Analytics
{
    /// <summary>A bounded Wilson score interval for a binomial proportion.</summary>
    public sealed class ConfidenceInterval
    {
        public double Lower { get; }
        public double Upper { get; }

        /// <summary>Stores bounds calculated by the validated Wilson implementation.</summary>
        internal ConfidenceInterval(double lower, double upper) { Lower = lower; Upper = upper; }
    }

    /// <summary>Calculates score intervals without the boundary failures of a normal approximation.</summary>
    public static class WilsonInterval
    {
        /// <summary>Uses the default 95 percent quantile; empty samples or nonfinite/nonpositive quantiles are invalid.</summary>
        public static ConfidenceInterval Calculate(int successes, int total, double z = 1.959963984540054)
        {
            if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (successes < 0 || successes > total) throw new ArgumentOutOfRangeException(nameof(successes));
            if (z <= 0 || double.IsNaN(z) || double.IsInfinity(z)) throw new ArgumentOutOfRangeException(nameof(z));
            var p = (double)successes / total;
            var scale = z / Math.Sqrt(total);
            var ratio = scale * scale;
            var inverse = 1 / (1 + ratio);
            var weight = double.IsPositiveInfinity(ratio) ? 1 : ratio * inverse;
            var center = p * inverse + weight / 2;
            var half = Math.Sqrt(weight * (p * (1 - p) * inverse + weight / 4));
            return new ConfidenceInterval(Math.Max(0, center - half), Math.Min(1, center + half));
        }
    }
}
