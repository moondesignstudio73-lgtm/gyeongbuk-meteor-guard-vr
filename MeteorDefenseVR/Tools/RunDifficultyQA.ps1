param(
    [ValidateSet('Prepare','Smoke','Regression','Soak','Build')][string]$Phase = 'Smoke',
    [string]$Run = 'first'
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$qaPath = Join-Path $projectPath 'Docs/DifficultyQA'
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
New-Item -ItemType Directory -Path $qaPath -Force | Out-Null
function Invoke-Editor([string[]]$Extra) {
    $arguments = @('-batchmode','-projectPath',('"'+$projectPath+'"')) + $Extra
    $editor = Start-Process -FilePath $unityPath -ArgumentList $arguments -WorkingDirectory $projectPath -WindowStyle Hidden -PassThru
    Write-Output "Unity Editor $Phase process $($editor.Id)"
    $editor.WaitForExit()
    if ($editor.ExitCode -ne 0) { throw "Editor exited with code $($editor.ExitCode)" }
}
if ($Phase -eq 'Prepare') {
    Invoke-Editor @('-quit','-executeMethod','MeteorDefenseVR.Editor.DifficultySetup.EnsureAssets','-logFile',('"'+$qaPath+'\'+$Run+'-assets.log"'))
    Invoke-Editor @('-executeMethod','MeteorDefenseVR.Editor.PcTestSetup.BakePresentationFont','-logFile',('"'+$qaPath+'\'+$Run+'-font.log"'))
} elseif ($Phase -eq 'Build') {
    Invoke-Editor @('-executeMethod','MeteorDefenseVR.Editor.PcTestSetup.BuildWindowsCurrentScene','-logFile',('"'+$qaPath+'\'+$Run+'-build.log"'))
} else {
    $xmlPath = Join-Path $qaPath "$Run-$Phase.xml"
    $arguments = @('-runTests','-testPlatform','EditMode','-testResults',('"'+$xmlPath+'"'),'-logFile',('"'+$qaPath+'\'+$Run+'-'+$Phase+'.log"'))
    if ($Phase -eq 'Smoke') { $arguments += @('-testFilter','MeteorDefenseVR.Tests.DifficultyTests') }
    if ($Phase -eq 'Soak') { $arguments += @('-testFilter','MeteorDefenseVR.Tests.DifficultySoakTests','--difficulty-soak') }
    Invoke-Editor $arguments
    [xml]$results = Get-Content -LiteralPath $xmlPath -Raw
    Write-Output "$Phase passed=$($results.'test-run'.passed) failed=$($results.'test-run'.failed) skipped=$($results.'test-run'.skipped)"
    if ([int]$results.'test-run'.failed -ne 0) { throw 'Test failure' }
}
