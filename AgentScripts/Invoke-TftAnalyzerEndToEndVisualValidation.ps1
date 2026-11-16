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
$entry = if ($ProbeInitializationFailure) { 'EndToEndScopedVisualValidation.RunInitializationFailureProbe' }
    elseif ($ProbeFailure) { 'EndToEndScopedVisualValidation.RunFailureProbe' } else { 'EndToEndScopedVisualValidation.Run' }
$resultFileName = if ($ProbeInitializationFailure) { 'e2e-visual-initialization-failure-command.json' }
    elseif ($ProbeFailure) { 'e2e-visual-failure-command.json' } else { 'e2e-visual-command.json' }
$resultPath = Join-Path $outputRoot $resultFileName
$cleanupPath = Join-Path $outputRoot 'e2e-visual-fallback-cleanup.json'
$validationFailure = $null

try {
    & $UnityPath command script run --project-path $projectRoot --file 'AgentScripts/TftAnalyzerEndToEndVisualValidation.cs' --entry $entry --timeout 45 --format json |
        Set-Content -LiteralPath $resultPath
    $commandExit = $LASTEXITCODE
    $report = if ($commandExit -eq 0) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json }
    if ($commandExit -ne 0 -or -not $report.result.Passed) {
        throw 'End-to-end visual validation failed. Inspect the bounded report in Library/TftAnalyzerValidation.'
    }
    $report.result | ConvertTo-Json -Depth 12
}
catch { $validationFailure = $_ }
finally {
    # Exact per-run ID/name plus baseline native-frame and focus evidence; never bulk-close user windows.
    & $UnityPath command script run --project-path $projectRoot --file 'AgentScripts/TftAnalyzerEndToEndVisualValidation.cs' --entry EndToEndScopedVisualValidation.CleanupLastOwned --timeout 10 --format json |
        Set-Content -LiteralPath $cleanupPath
    $cleanupExit = $LASTEXITCODE
    $cleanup = if ($cleanupExit -eq 0) { Get-Content -LiteralPath $cleanupPath -Raw | ConvertFrom-Json }
    if ($cleanupExit -ne 0 -or -not $cleanup.result.Verified) {
        throw 'End-to-end owned-window/native-frame cleanup was not verified. Inspect e2e-visual-fallback-cleanup.json; do not overlap another capture.'
    }
}
if ($validationFailure) { throw $validationFailure }
