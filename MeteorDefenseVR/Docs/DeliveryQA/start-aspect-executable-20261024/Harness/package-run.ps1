$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$qa=Join-Path $project 'Docs/DeliveryQA/start-aspect-executable-20261024'
$source=(git rev-parse HEAD).Trim()
$build=Join-Path $project ('Builds/InstrumentedStartAspect_'+$source.Substring(0,7))
$zip=Join-Path $PSScriptRoot 'StartAspectInstrumented-test-only.zip'
Compress-Archive -Path (Join-Path $build '*') -DestinationPath $zip
$extract=Join-Path $project ('Builds/Verify/StartAspectInstrumented_'+$source.Substring(0,7))
if(Test-Path $extract){throw 'Extraction exists'}
Expand-Archive -LiteralPath $zip -DestinationPath $extract
$files=@(Get-ChildItem $build -File -Recurse)
foreach($file in $files){$relative=$file.FullName.Substring($build.Length+1);if((Get-FileHash $file.FullName).Hash -ne (Get-FileHash (Join-Path $extract $relative)).Hash){throw 'Extracted file differs'}}
[ordered]@{source=$source;publicBaseline='6d27c10ef6acc4b457f8fbfbac433235960a7358';instrumented=$true;define='START_ASPECT_QA';zip=$zip;sha256=(Get-FileHash $zip).Hash.ToLowerInvariant();bytes=(Get-Item $zip).Length;files=$files.Count;build=$build;extracted=$extract;identity=(Get-Content (Join-Path $build 'BUILD_IDENTITY.json') -Raw|ConvertFrom-Json)}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $qa 'instrumented-package.json')
$registryPath='HKCU:\Software\DefaultCompany\MeteorDefenseVR'
function ReadPrefs {
 $values=[ordered]@{}
 if(Test-Path $registryPath){$key=Get-Item $registryPath;foreach($name in @($key.GetValueNames()|Sort-Object)){$values[$name]=[ordered]@{kind=$key.GetValueKind($name).ToString();value=$key.GetValue($name)}}}
 return $values
}
function ReadRecords {
 $values=[ordered]@{}
 foreach($name in @('MeteorDefense-ranking-v1.json','MeteorDefense-ranking-v1.json.bak','MeteorDefense-ranking-v1.json.tmp')){
  $file=Join-Path $recordDir $name
  $values[$name]=if(Test-Path $file){(Get-FileHash $file).Hash}else{'absent'}
 }
 return $values
}
$recordDir=Join-Path $env:USERPROFILE 'AppData/LocalLow/DefaultCompany/MeteorDefenseVR'
$privateDir=Join-Path $PSScriptRoot ('private-'+$source.Substring(0,7))
New-Item -ItemType Directory -Path $privateDir -Force|Out-Null
$beforePrefs=ReadPrefs;$beforeRecords=ReadRecords
$beforePrefs|ConvertTo-Json -Depth 6|Set-Content (Join-Path $privateDir 'prefs-before.json')
$beforeRecords|ConvertTo-Json|Set-Content (Join-Path $privateDir 'records-before.json')
foreach($name in $beforeRecords.Keys){if($beforeRecords[$name] -ne 'absent'){Copy-Item (Join-Path $recordDir $name) (Join-Path $privateDir $name)}}
$started=[DateTime]::UtcNow.ToString('O');$log=Join-Path $qa ('player-'+$source.Substring(0,7)+'.log')
$p=Start-Process -FilePath (Join-Path $extract 'MeteorDefenseVR.exe') -ArgumentList @('--pc-start-aspect-qa','--no-webcam','-batchmode','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$finished=$p.WaitForExit(120000)
if(-not $finished){$p.Kill();$p.WaitForExit()}
$exitCode=$p.ExitCode
[ordered]@{nativeExitCode=$exitCode;timedOut=(-not $finished);startedUtc=$started;endedUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content (Join-Path $qa ('process-'+$source.Substring(0,7)+'.json'))
$afterPrefs=ReadPrefs;$afterRecords=ReadRecords
$afterPrefs|ConvertTo-Json -Depth 6|Set-Content (Join-Path $privateDir 'prefs-after.json')
$afterRecords|ConvertTo-Json|Set-Content (Join-Path $privateDir 'records-after.json')
$changes=@();$restored=@()
foreach($name in @(($beforePrefs.Keys+$afterPrefs.Keys)|Sort-Object -Unique)){
 if(($beforePrefs[$name]|ConvertTo-Json -Depth 5 -Compress) -ne ($afterPrefs[$name]|ConvertTo-Json -Depth 5 -Compress)){
  $changes+=$name
  if($name -match '^unity\.player_session|^unity_connect\..*session'){continue}
  $key=Get-Item $registryPath
  if($beforePrefs.Contains($name)){$key.SetValue($name,$beforePrefs[$name].value,[Microsoft.Win32.RegistryValueKind]$beforePrefs[$name].kind)}else{$key.DeleteValue($name,$false)}
  $restored+=$name
 }
}
$recordChanges=@()
foreach($name in $beforeRecords.Keys){if($beforeRecords[$name] -ne $afterRecords[$name]){
 $recordChanges+=$name
 if($beforeRecords[$name] -eq 'absent'){Remove-Item -LiteralPath (Join-Path $recordDir $name)}else{Copy-Item -LiteralPath (Join-Path $privateDir $name) -Destination (Join-Path $recordDir $name) -Force}
}}
$finalPrefs=ReadPrefs;$finalRecords=ReadRecords
foreach($name in @(($beforePrefs.Keys+$finalPrefs.Keys)|Sort-Object -Unique)){
 if($name -match '^unity\.player_session|^unity_connect\..*session'){continue}
 if(($beforePrefs[$name]|ConvertTo-Json -Depth 5 -Compress) -ne ($finalPrefs[$name]|ConvertTo-Json -Depth 5 -Compress)){throw 'Preferences restore mismatch'}
}
foreach($name in $beforeRecords.Keys){if($beforeRecords[$name] -ne $finalRecords[$name]){throw 'Record restore mismatch'}}
$proof=Join-Path $extract 'QA/StartAspect'
$destination=Join-Path $qa ('Instrumented-'+$source.Substring(0,7))
if(Test-Path $proof){Copy-Item -LiteralPath $proof -Destination $destination -Recurse}
$runtime=if(Test-Path (Join-Path $proof 'runtime.txt')){Get-Content (Join-Path $proof 'runtime.txt') -Raw}else{'missing'}
$count=@(Get-ChildItem $proof -Recurse -Filter stability-game-render.png -ErrorAction SilentlyContinue).Count
$passed=$finished -and $exitCode -eq 0 -and $runtime.Contains('passed=True') -and $count -eq 10
[ordered]@{passed=$passed;nativeExitCode=$exitCode;timedOut=(-not $finished);startedUtc=$started;endedUtc=[DateTime]::UtcNow.ToString('O');images=$count;registryChangedNames=$changes;nonSessionRestoredNames=$restored;recordRestoredNames=$recordChanges;preservationVerified=$true;runtime=$runtime}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $qa ('run-'+$source.Substring(0,7)+'.json'))
if(-not $passed){throw 'Start aspect fixture failed; evidence and process exit preserved'}
Write-Output 'Start aspect instrumented fixture passed; 10 images; native exit and preservation recorded'
