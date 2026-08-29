param([int]$ExistingTestProcessId, [string]$RegressionName = 'final-regression-tests.xml', [string]$CapturePrefix = 'motion')
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$qaPath = Join-Path $projectPath 'Docs/UiMotionQA'
if ($ExistingTestProcessId -gt 0) {
    $running = Get-Process -Id $ExistingTestProcessId -ErrorAction SilentlyContinue
    if ($running) { Write-Output 'Waiting for the existing final regression run.'; $running.WaitForExit() }
}
[xml]$regression = Get-Content -LiteralPath (Join-Path $qaPath $RegressionName) -Raw
Write-Output "Regression: passed=$($regression.'test-run'.passed), failed=$($regression.'test-run'.failed), skipped=$($regression.'test-run'.skipped)"
if ([int]$regression.'test-run'.failed -ne 0) { throw 'Regression failed. Do not continue to release.' }

function Invoke-Editor([string[]]$ExtraArguments) {
    $arguments = @('-batchmode','-projectPath',('"' + $projectPath + '"')) + $ExtraArguments
    $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -WorkingDirectory $projectPath -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity failed with code $($process.ExitCode)" }
}
$captureRuns = @(1..3 | ForEach-Object { "$CapturePrefix-$_" })
foreach ($run in $captureRuns) {
    Write-Output "CPU Profiler capture: $run"
    $results = Join-Path $qaPath ($run + '-tests.xml')
    $log = Join-Path $qaPath ($run + '.log')
    Invoke-Editor @('-runTests','-testPlatform','EditMode','-testFilter','MeteorDefenseVR.Tests.StartupHitchProfilerTests',
        '-testResults',('"' + $results + '"'),'-logFile',('"' + $log + '"'),("--hitch-profile=$run"),'--no-webcam','--gaze=mouse')
    [xml]$capture = Get-Content -LiteralPath $results -Raw
    if ([int]$capture.'test-run'.failed -ne 0 -or [int]$capture.'test-run'.passed -ne 1) { throw "Profiler capture failed: $run" }
    Write-Output "$run passed."
}
& (Join-Path $PSScriptRoot 'CompareUiMotion.ps1') -Runs (@('after-1','after-2','after-3') + $captureRuns)
Write-Output 'Building Windows from the existing authored scenes; not running the player.'
Invoke-Editor @('-executeMethod','MeteorDefenseVR.Editor.PcTestSetup.BuildWindowsCurrentScene',
    '-logFile',('"' + (Join-Path $qaPath 'windows-build.log') + '"'))
Write-Output 'Windows build completed. Player was not launched.'
