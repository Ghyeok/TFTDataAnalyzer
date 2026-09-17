using NUnit.Framework;
using UnityEditor;

namespace TFTAnalyzer.Tests.Ingestion
{
    public sealed class ProjectIdentityTests
    {
        [Test]
        public void ProductName_IsTftDataAnalyzer()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo("TFT Data Analyzer"));
        }
    }
}
