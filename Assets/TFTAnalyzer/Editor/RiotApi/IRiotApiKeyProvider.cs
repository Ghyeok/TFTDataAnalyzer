namespace TFTAnalyzer.Editor.RiotApi
{
    /// <summary>Supplies credentials from process memory without exposing them through diagnostic properties.</summary>
    public interface IRiotApiKeyProvider
    {
        bool IsConfigured { get; }
        /// <summary>Returns a valid key or throws an error containing no credential values.</summary>
        string GetRequiredKey();
    }
}
