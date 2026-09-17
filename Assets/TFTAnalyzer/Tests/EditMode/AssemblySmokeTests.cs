using NUnit.Framework;

namespace TFTAnalyzer.Tests
{
    public sealed class AssemblySmokeTests
    {
        [Test]
        public void CoreAssembly_IsLoadable()
        {
            Assert.That(
                typeof(TFTAnalyzer.Domain.StaticCatalog).Assembly.GetName().Name,
                Is.EqualTo("TFTAnalyzer.Core"));
        }
    }
}
