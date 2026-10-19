using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TFTAnalyzer.Editor.Compliance
{
    public sealed class ComplianceBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        private readonly ComplianceSettingsStore _store;
        private readonly Func<DateTimeOffset> _clock;
        public ComplianceBuildGuard() : this(new ComplianceSettingsStore(Path.GetDirectoryName(UnityEngine.Application.dataPath)), () => DateTimeOffset.UtcNow) { }
        public ComplianceBuildGuard(ComplianceSettingsStore store, Func<DateTimeOffset> clock)
        { _store = store ?? throw new ArgumentNullException(nameof(store)); _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }
        public void ValidateForBuild()
        {
            var read = _store.Load();
            if (read.State != ComplianceReadState.Valid || !CompliancePolicy.CanPublish(read.Settings, _clock()))
                throw new BuildFailedException("Player 빌드를 진행할 수 없습니다. Project/TFT Data Analyzer/Compliance에서 정책 검토일과 Portal 등록 상태를 확인하고 명시적으로 저장해 주세요.");
        }
        public void OnPreprocessBuild(BuildReport report) => ValidateForBuild();
    }
}
