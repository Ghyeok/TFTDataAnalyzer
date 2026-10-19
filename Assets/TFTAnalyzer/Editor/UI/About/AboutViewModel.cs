using TFTAnalyzer.Editor.Compliance;

namespace TFTAnalyzer.Editor.UI.About
{
    public sealed class AboutViewModel
    {
        private readonly ComplianceSettingsStore _store;
        public AboutViewModel(ComplianceSettingsStore store) => _store = store ?? throw new System.ArgumentNullException(nameof(store));
        public ComplianceSettingsReadResult Refresh() => _store.Load();
    }
}
