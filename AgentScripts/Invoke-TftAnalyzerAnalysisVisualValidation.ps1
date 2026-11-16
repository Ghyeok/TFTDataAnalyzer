param(
    [string]$UnityPath = (Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'),
    [switch]$ProbeFailure,
    [switch]$ProbeInitializationFailure,
    [switch]$MinimumSamples
)

$ErrorActionPreference = 'Stop'
if ($ProbeFailure -and $ProbeInitializationFailure) { throw 'Choose one cleanup failure probe per invocation.' }
if ($MinimumSamples -and ($ProbeFailure -or $ProbeInitializationFailure)) { throw 'Choose minimum-sample capture or one cleanup probe.' }
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $projectRoot 'Library\TftAnalyzerValidation'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$entry = if ($ProbeInitializationFailure) { 'AnalysisScopedVisualValidation.RunInitializationFailureProbe' }
    elseif ($ProbeFailure) { 'AnalysisScopedVisualValidation.RunFailureProbe' }
    elseif ($MinimumSamples) { 'AnalysisScopedVisualValidation.RunMinimumSamples' } else { 'AnalysisScopedVisualValidation.Run' }
$resultFileName = if ($ProbeInitializationFailure) { 'analysis-visual-initialization-failure-command.json' }
    elseif ($ProbeFailure) { 'analysis-visual-failure-command.json' }
    elseif ($MinimumSamples) { 'analysis-minimum-visual-command.json' } else { 'analysis-visual-command.json' }
$resultPath = Join-Path $outputRoot $resultFileName
$cleanupPath = Join-Path $outputRoot 'analysis-visual-fallback-cleanup.json'
$validationFailure = $null

try {
    & $UnityPath command script run --project-path $projectRoot --file 'AgentScripts/TftAnalyzerAnalysisVisualValidation.cs' --entry $entry --timeout 15 --format json |
        Set-Content -LiteralPath $resultPath
    $commandExit = $LASTEXITCODE
    $report = if ($commandExit -eq 0) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json }
    if ($commandExit -ne 0 -or -not $report.result.Passed) {
        throw 'Analysis visual validation failed. Inspect the bounded report in Library/TftAnalyzerValidation.'
    }
    $report.result | ConvertTo-Json -Depth 12
}
catch { $validationFailure = $_ }
finally {
    # Exact per-run ID/name plus baseline native-frame and focus evidence; never bulk-close user windows.
    & $UnityPath command script run --project-path $projectRoot --file 'AgentScripts/TftAnalyzerAnalysisVisualValidation.cs' --entry AnalysisScopedVisualValidation.CleanupLastOwned --timeout 10 --format json |
        Set-Content -LiteralPath $cleanupPath
    $cleanupExit = $LASTEXITCODE
    $cleanup = if ($cleanupExit -eq 0) { Get-Content -LiteralPath $cleanupPath -Raw | ConvertFrom-Json }
    if ($cleanupExit -ne 0 -or -not $cleanup.result.Verified) {
        throw 'Analysis owned-window/native-frame cleanup was not verified. Inspect analysis-visual-fallback-cleanup.json; do not overlap another capture.'
    }
}
if ($validationFailure) { throw $validationFailure }
