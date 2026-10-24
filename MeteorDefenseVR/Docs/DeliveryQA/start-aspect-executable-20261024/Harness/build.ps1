$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$repo=Split-Path $project -Parent
$settings='MeteorDefenseVR/ProjectSettings/ProjectSettings.asset'
$qa=Join-Path $project 'Docs/DeliveryQA/start-aspect-executable-20261024'
Copy-Item (Join-Path $repo $settings) (Join-Path $qa 'user-ProjectSettings.backup')
$settingsStash=$null
git -C $repo diff --quiet -- $settings
if($LASTEXITCODE -ne 0){
 git -C $repo stash push -m 'Preserve user settings during audio priority build' -- $settings
 if($LASTEXITCODE -ne 0){throw 'Stash failed'}
 $settingsStash=(git -C $repo rev-parse 'stash@{0}').Trim()
 $settingsStash | Set-Content (Join-Path $qa 'user-settings-stash.txt')
}
try {
 if(@(git -C $repo status --porcelain).Count -ne 0){throw 'Build must start clean'}
 $log=Join-Path $qa 'build.log'
 $process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','MeteorDefenseVR.Editor.PcTestSetup.BuildStartAspectInstrumented','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
 $process.WaitForExit()
 if($process.ExitCode -ne 0){throw 'Build failed'}
} finally {
 git -C $repo diff --quiet -- $settings
 if($LASTEXITCODE -ne 0){throw 'Settings changed during build; inspect before restoring'}
 git -C $repo add -- $settings
 git -C $repo diff --cached --quiet -- $settings
 if($LASTEXITCODE -ne 0){throw "Unexpected staged settings change"}
 if($settingsStash){git -C $repo stash apply $settingsStash;if($LASTEXITCODE -ne 0){throw 'Settings restore failed'}}
 $a=[IO.File]::ReadAllText((Join-Path $repo $settings)).Replace("`r`n","`n")
 $b=[IO.File]::ReadAllText((Join-Path $qa 'user-ProjectSettings.backup')).Replace("`r`n","`n")
 if($a -ne $b){throw 'User settings mismatch'}
 Write-Output 'User settings preserved and verified'
}














