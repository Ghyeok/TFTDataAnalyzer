using System;
using NUnit.Framework;
using TFTAnalyzer.Analytics;

namespace TFTAnalyzer.Tests.Analytics
{
    /// <summary>Checks independently known Wilson score vectors and invalid sample parameters.</summary>
    public sealed class WilsonIntervalTests
    {
        /// <summary>Pins the approved 56-of-100 default 95 percent score interval.</summary>
        [Test]
        public void KnownFiftySixOfOneHundredVector()
        {
            var interval = WilsonInterval.Calculate(56, 100);
            Assert.That(interval.Lower, Is.EqualTo(0.4623).Within(0.00005));
            Assert.That(interval.Upper, Is.EqualTo(0.6533).Within(0.00005));
        }

        /// <summary>Verifies boundary and small-sample vectors without a normal-approximation shortcut.</summary>
        [TestCase(0, 100, 0, 0.0369934982)]
        [TestCase(100, 100, 0.9630065018, 1)]
        [TestCase(0, 1, 0, 0.7934506856)]
        [TestCase(1, 1, 0.2065493144, 1)]
        [TestCase(50, 100, 0.4038315304, 0.5961684696)]
        public void BoundaryAndSmallSampleVectors(int successes, int total, double lower, double upper)
        {
            var interval = WilsonInterval.Calculate(successes, total);
            Assert.That(interval.Lower, Is.EqualTo(lower).Within(0.000000001));
            Assert.That(interval.Upper, Is.EqualTo(upper).Within(0.000000001));
        }

        /// <summary>Rejects empty totals and numerators outside their denominator.</summary>
        [TestCase(0, 0)]
        [TestCase(0, -1)]
        [TestCase(-1, 100)]
        [TestCase(101, 100)]
        public void InvalidCountsAreRejected(int successes, int total)
            => Assert.Throws<ArgumentOutOfRangeException>(() => WilsonInterval.Calculate(successes, total));

        /// <summary>Rejects undefined confidence parameters instead of propagating NaN intervals.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidConfidenceParameterIsRejected(double z)
            => Assert.Throws<ArgumentOutOfRangeException>(() => WilsonInterval.Calculate(1, 2, z));

        /// <summary>Supports a supplied score quantile and finite extreme quantiles without overflow.</summary>
        [Test]
        public void CustomAndExtremeQuantilesRemainFinite()
        {
            var custom = WilsonInterval.Calculate(1, 2, 1);
            Assert.That(custom.Lower, Is.EqualTo(0.2113248654).Within(0.000000001));
            Assert.That(custom.Upper, Is.EqualTo(0.7886751346).Within(0.000000001));
            var wide = WilsonInterval.Calculate(1, 2, double.MaxValue);
            Assert.That(wide.Lower, Is.EqualTo(0));
            Assert.That(wide.Upper, Is.EqualTo(1));
            var large = WilsonInterval.Calculate(int.MaxValue, int.MaxValue);
            Assert.That(large.Lower, Is.InRange(0.99999999, 1));
            Assert.That(large.Upper, Is.EqualTo(1).Within(1e-15));
        }
    }
}
