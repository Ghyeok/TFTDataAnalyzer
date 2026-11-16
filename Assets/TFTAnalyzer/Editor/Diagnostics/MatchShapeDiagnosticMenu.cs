using System;
using System.IO;
using System.Threading;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using UnityEditor;
using UnityEngine;

namespace TFTAnalyzer.Editor.Diagnostics
{
    /// <summary>
    /// Thin Editor wrapper for the one-time real-response shape diagnostic. All logic lives in
    /// MatchShapeDiagnosticEntryPoint; this class only wires production dependencies and dialogs.
    /// </summary>
    internal static class MatchShapeDiagnosticMenu
    {
        private const string Title = "경기 형태 진단";
        private static bool _running;

        [MenuItem("TFT Analyzer/Diagnostics/Run Match Shape Diagnostic (max 21 requests)...")]
        private static async void Run()
        {
            if (_running) return;
            _running = true;
            var cancellation = new CancellationTokenSource();
            EditorApplication.CallbackFunction poll = () =>
            {
                if (EditorUtility.DisplayCancelableProgressBar(Title, "요청 진행 중입니다(최대 21회). 취소하면 남은 요청을 보내지 않습니다.", 0.5f))
                    cancellation.Cancel();
            };
            MatchShapeRunResult result = null;
            MatchShapeReportFileSink sink = null;
            try
            {
                var root = Path.GetDirectoryName(Application.dataPath);
                sink = new MatchShapeReportFileSink(root, new[] { new AnalyzerStorePaths(root).RootDirectory });
                var reportSink = sink;
                var entry = new MatchShapeDiagnosticEntryPoint(
                    new PublishedCatalogMatchShapeSource(new StaticCatalogPaths(root),
                        () => AssetDatabase.LoadAssetAtPath<CatalogIndex>(StaticCatalogPaths.CatalogIndexAssetPath)),
                    () => new MatchShapeDiagnostic(new UnityRiotApiTransport(), new EnvironmentRiotApiKeyProvider(),
                        new EnvironmentSeedPuuidProvider(), reportSink,
                        new DialogConfirmation(() => EditorApplication.update += poll), new ReloadLock()));
                result = await entry.RunAsync(cancellation.Token);
            }
            catch { result = null; }
            finally
            {
                // The poll and progress bar are always gone before the result dialog is shown.
                EditorApplication.update -= poll;
                EditorUtility.ClearProgressBar();
                cancellation.Dispose();
                _running = false;
            }
            EditorUtility.DisplayDialog(Title, result == null
                ? "진단을 완료하지 못했습니다. 세부 정보는 표시하지 않습니다."
                : Describe(result, sink?.LastReportPath), "확인");
        }

        /// <summary>Shows only controlled codes, counts and the report file name.</summary>
        private static string Describe(MatchShapeRunResult result, string reportPath)
            => "상태: " + result.Status + "\n사유: " + result.ReasonCode +
               "\n물리 전송: 목록 " + result.ListSends + "회, 상세 " + result.DetailSends + "회" +
               "\n보고서: " + (result.ReportPublished && reportPath != null
                   ? "Library/TftAnalyzerDiagnostics/match-shape/" + Path.GetFileName(reportPath)
                   : "없음 (" + result.ReportFailureCode + ")") +
               "\n결과 분류: " + result.Classification +
               "\n실행 소모: " + (result.RunConsumed ? "예" : "아니요");

        private sealed class DialogConfirmation : IMatchShapeConfirmation
        {
            private readonly Action _confirmed;
            public DialogConfirmation(Action confirmed) { _confirmed = confirmed; }

            public bool ConfirmUnityRestartedAfterKeyRenewal(string summary)
                => EditorUtility.DisplayDialog(Title, summary, "예, 재시작했습니다", "아니요, 중단");

            public bool ConfirmNoCollectionInProgress()
            {
                var confirmed = EditorUtility.DisplayDialog(Title,
                    "진행 중인 경기 수집이 없습니까?\n수집 탭에서 수집이 실행 중이면 '아니요'를 선택해 주세요.",
                    "예, 수집 중이 아닙니다", "아니요, 중단");
                // The cancelable progress bar appears only after both confirmations.
                if (confirmed) _confirmed();
                return confirmed;
            }
        }

        private sealed class ReloadLock : IAssemblyReloadLock
        {
            public void Lock() => EditorApplication.LockReloadAssemblies();
            public void Unlock() => EditorApplication.UnlockReloadAssemblies();
        }
    }
}
