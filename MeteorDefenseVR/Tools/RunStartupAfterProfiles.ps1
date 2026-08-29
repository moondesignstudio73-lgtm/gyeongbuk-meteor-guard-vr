$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$qaPath = Join-Path $projectPath 'Docs/StartupHitchQA'
$resultsPath = Join-Path $qaPath 'focused-ready-tests.xml'
$deadline = [DateTime]::UtcNow.AddMinutes(12)
while ((-not (Test-Path -LiteralPath $resultsPath)) -or (Get-Process Unity -ErrorAction SilentlyContinue)) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Regression/Editor wait timed out; no new Editor was launched.' }
    Start-Sleep -Seconds 3
}
[xml]$regression = Get-Content -LiteralPath $resultsPath
if ($regression.'test-run'.result -ne 'Passed') { throw 'Regression did not pass. No profiling runs launched.' }
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
foreach ($run in @('after-1','after-2','after-3')) {
    $xmlPath = Join-Path $qaPath "$run-tests.xml"
    $logPath = Join-Path $qaPath "$run.log"
    if ((Test-Path -LiteralPath $xmlPath) -or (Test-Path -LiteralPath (Join-Path $qaPath $run))) {
        throw "Refusing to overwrite existing capture $run"
    }
    Write-Output "Starting fresh Editor capture: $run"
    $arguments = '-batchmode -projectPath "{0}" -runTests -testPlatform EditMode -testFilter MeteorDefenseVR.Tests.StartupHitchProfilerTests --hitch-profile={1} --no-webcam --gaze=mouse -testResults "{2}" -logFile "{3}"' -f $projectPath,$run,$xmlPath,$logPath
    $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $xmlPath)) { throw "Capture failed: $run" }
    [xml]$result = Get-Content -LiteralPath $xmlPath
    if ($result.'test-run'.result -ne 'Passed') { throw "Profiler test failed: $run" }
    Write-Output "Passed: $run"
}
& (Join-Path $PSScriptRoot 'CompareStartupHitches.ps1')
