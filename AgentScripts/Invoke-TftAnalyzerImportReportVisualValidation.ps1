param(
    [string]$UnityPath = (Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'),
    [switch]$ProbeFailure,
    [switch]$ProbeInitializationFailure
)

$ErrorActionPreference = 'Stop'
if ($ProbeFailure -and $ProbeInitializationFailure) { throw 'Choose one cleanup failure probe per invocation.' }
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $projectRoot 'Library\TftAnalyzerValidation'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$entry = if ($ProbeInitializationFailure) { 'ImportReportScopedVisualValidation.RunInitializationFailureProbe' }
    elseif ($ProbeFailure) { 'ImportReportScopedVisualValidation.RunFailureProbe' } else { 'ImportReportScopedVisualValidation.Run' }
$resultFileName = if ($ProbeInitializationFailure) { 'import-report-visual-initialization-failure-command.json' }
    elseif ($ProbeFailure) { 'import-report-visual-failure-command.json' } else { 'import-report-visual-command.json' }
$resultPath = Join-Path $outputRoot $resultFileName
$cleanupPath = Join-Path $outputRoot 'import-report-visual-fallback-cleanup.json'
$validationFailure = $null

try {
    & $UnityPath command run_script --project-path $projectRoot --file 'AgentScripts/TftAnalyzerImportReportVisualValidation.cs' --entry $entry --timeout_ms 15000 --format json |
        Set-Content -LiteralPath $resultPath
    $commandExit = $LASTEXITCODE
    $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($commandExit -ne 0 -or -not $report.success -or -not $report.data.result.success -or -not $report.data.result.result.Passed) {
        throw 'Import report visual validation failed. Inspect the bounded report in Library/TftAnalyzerValidation.'
    }
    $report.data.result.result | ConvertTo-Json -Depth 12
}
catch { $validationFailure = $_ }
finally {
    # Exact per-run ID/name plus baseline native-frame and focus evidence; never bulk-close user windows.
    & $UnityPath command run_script --project-path $projectRoot --file 'AgentScripts/TftAnalyzerImportReportVisualValidation.cs' --entry ImportReportScopedVisualValidation.CleanupLastOwned --timeout_ms 10000 --format json |
        Set-Content -LiteralPath $cleanupPath
    $cleanupExit = $LASTEXITCODE
    $cleanup = Get-Content -LiteralPath $cleanupPath -Raw | ConvertFrom-Json
    if ($cleanupExit -ne 0 -or -not $cleanup.success -or -not $cleanup.data.result.success -or -not $cleanup.data.result.result.Verified) {
        throw 'Import report owned-window/native-frame cleanup was not verified. Inspect import-report-visual-fallback-cleanup.json; do not overlap another capture.'
    }
}
if ($validationFailure) { throw $validationFailure }
