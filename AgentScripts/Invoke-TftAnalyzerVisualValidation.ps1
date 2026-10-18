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
$entry = if ($ProbeInitializationFailure) { 'CollectionScopedVisualValidation.RunInitializationFailureProbe' }
    elseif ($ProbeFailure) { 'CollectionScopedVisualValidation.RunFailureProbe' } else { 'CollectionScopedVisualValidation.Run' }
$resultFileName = if ($ProbeInitializationFailure) { 'collection-visual-initialization-failure-command.json' }
    elseif ($ProbeFailure) { 'collection-visual-failure-command.json' } else { 'collection-visual-command.json' }
$resultPath = Join-Path $outputRoot $resultFileName

try {
    & $UnityPath command run_script --project-path $projectRoot --file 'AgentScripts/TftAnalyzerVisualValidation.cs' --entry $entry --timeout_ms 15000 --format json |
        Set-Content -LiteralPath $resultPath
    $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $report.success -or -not $report.data.result.success -or -not $report.data.result.result.Passed) {
        throw 'Scoped visual validation failed. Inspect the bounded validation report in Library/TftAnalyzerValidation.'
    }
    $report.data.result.result | ConvertTo-Json -Depth 8
}
finally {
    # Exact recorded instance ID plus a per-run generated name; existing user windows never match.
    & $UnityPath command run_script --project-path $projectRoot --file 'AgentScripts/TftAnalyzerVisualValidation.cs' --entry CollectionScopedVisualValidation.CleanupLastOwned --timeout_ms 10000 --format json |
        Set-Content -LiteralPath (Join-Path $outputRoot 'collection-visual-fallback-cleanup.json')
    $cleanup = Get-Content -LiteralPath (Join-Path $outputRoot 'collection-visual-fallback-cleanup.json') -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $cleanup.success -or -not $cleanup.data.result.success -or $cleanup.data.result.result.OwnedRemaining -ne 0) {
        throw 'Owned EditorWindow cleanup was not verified. Inspect collection-visual-fallback-cleanup.json.'
    }
}
