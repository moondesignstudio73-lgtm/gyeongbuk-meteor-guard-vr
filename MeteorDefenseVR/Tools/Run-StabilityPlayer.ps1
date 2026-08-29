param(
    [ValidateSet('Soak','Webcam')][string]$Mode = 'Soak',
    [ValidateRange(1,100)][int]$Rounds = 30,
    [ValidatePattern('^[a-z0-9-]+$')][string]$Label = 'soak-30'
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $projectRoot 'Builds\Windows'
$reportRoot = Join-Path $projectRoot 'Docs\StabilityQA'
$playerPath = Join-Path $buildRoot 'MeteorDefenseVR.exe'
$logPath = Join-Path $reportRoot ($Label + '.log')
$reportPath = Join-Path $reportRoot ($Label + '-host.json')
$qaPath = Join-Path $buildRoot 'QA\pc-soak.json'
$appDataRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\MeteorDefenseVR'
function Read-AppFileMetadata {
    $files = @{}
    foreach ($directory in @($appDataRoot, (Join-Path $buildRoot 'QA'))) {
        if (Test-Path -LiteralPath $directory) {
            Get-ChildItem -LiteralPath $directory -File -Recurse | ForEach-Object {
                $files[$_.FullName] = ($_.Length.ToString() + ':' + $_.LastWriteTimeUtc.Ticks.ToString())
            }
        }
    }
    return $files
}
$filesBefore = Read-AppFileMetadata
$runStart = [datetime]::UtcNow
$arguments = @('-screen-width','1280','-screen-height','720','-logFile',('"' + $logPath + '"'))
if ($Mode -eq 'Soak') { $arguments += @('--pc-soak',('--pc-soak-rounds=' + $Rounds),'--no-webcam') }
else { $arguments += '--pc-webcam-stability' }
$player = Start-Process -FilePath $playerPath -ArgumentList $arguments -WorkingDirectory $buildRoot -WindowStyle Hidden -PassThru
$snapshots = [System.Collections.Generic.List[object]]::new()
$destinations = [System.Collections.Generic.HashSet[string]]::new()
$networkErrors = [System.Collections.Generic.HashSet[string]]::new()
$lastRound = 0
Write-Output ('Started scoped QA player PID=' + $player.Id + ' Mode=' + $Mode)
while (-not $player.HasExited) {
    $player.Refresh()
    if ($player.HasExited) { break }
    $tcpErrors = @()
    $tcp = @(Get-NetTCPConnection -OwningProcess $player.Id -ErrorAction SilentlyContinue -ErrorVariable tcpErrors)
    foreach ($failure in $tcpErrors) {
        if ($failure.FullyQualifiedErrorId -notlike '*CmdletizationQuery_NotFound*') { [void]$networkErrors.Add($failure.FullyQualifiedErrorId) }
    }
    foreach ($connection in $tcp) {
        if ($connection.RemoteAddress -notin @('0.0.0.0','::','127.0.0.1','::1')) {
            [void]$destinations.Add($connection.RemoteAddress + ':' + $connection.RemotePort)
        }
    }
    $snapshots.Add([pscustomobject]@{
        elapsedSeconds = [math]::Round(([datetime]::UtcNow - $runStart).TotalSeconds,2)
        privateBytes = $player.PrivateMemorySize64
        workingSetBytes = $player.WorkingSet64
        tcpConnections = $tcp.Count
    })
    if ($Mode -eq 'Soak' -and (Test-Path -LiteralPath $qaPath)) {
        try {
            $qa = Get-Content -LiteralPath $qaPath -Raw | ConvertFrom-Json
            if ([datetime]$qa.startedUtc -ge $runStart.AddSeconds(-2) -and $qa.rounds.Count -gt $lastRound) {
                $lastRound = $qa.rounds.Count
                $round = $qa.rounds[-1]
                Write-Output ("Round {0}/{1}: passed={2}, fps={3:N2}, p99={4:N2}ms, renderFrames={5}, duplicates={6}" -f $lastRound,$Rounds,$round.passed,$round.averageFps,$round.p99FrameMs,$round.renderedFrames,$round.subscriptions.duplicateCallbacks)
            }
        } catch { } # The player may be replacing its JSON during a sample.
    }
    Start-Sleep -Seconds 3
}
$player.WaitForExit()
$filesAfter = Read-AppFileMetadata
$changedFiles = @($filesAfter.Keys | Where-Object { -not $filesBefore.ContainsKey($_) -or $filesBefore[$_] -ne $filesAfter[$_] } | ForEach-Object {
    if ($_.StartsWith($appDataRoot)) { 'LocalLow/' + $_.Substring($appDataRoot.Length + 1) }
    else { 'BuildQA/' + $_.Substring((Join-Path $buildRoot 'QA').Length + 1) }
})
$hostReport = [ordered]@{
    startedUtc = $runStart.ToString('O')
    completedUtc = [datetime]::UtcNow.ToString('O')
    playerPid = $player.Id
    exitCode = $player.ExitCode
    mode = $Mode
    requestedRounds = $Rounds
    networkScope = 'Owning-PID TCP snapshots about every 3 seconds, not packet capture or proof against short-lived connections; no image or network payload recorded'
    networkProbeErrors = @($networkErrors | ForEach-Object { $_ })
    observedRemoteEndpoints = @($destinations | ForEach-Object { $_ })
    fileObservationScope = 'Metadata only, application LocalLow directory and build QA directory; not a whole-system filesystem trace'
    changedApplicationFiles = $changedFiles
    snapshots = @($snapshots.ToArray())
}
$hostReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output ('Player exited ' + $player.ExitCode + '; host evidence: ' + $reportPath)
exit $player.ExitCode
