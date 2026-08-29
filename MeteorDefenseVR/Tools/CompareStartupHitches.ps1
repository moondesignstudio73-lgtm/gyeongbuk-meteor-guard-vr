param(
    [string[]]$Runs = @('before-1','before-2','before-3','after-1','after-2','after-3'),
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$qaPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Docs/StartupHitchQA'
$comparisonPath = if ($OutputDirectory) { $OutputDirectory } else { $qaPath }
New-Item -ItemType Directory -Path $comparisonPath -Force | Out-Null
$rows = foreach ($run in $Runs) {
    $path = Join-Path $qaPath "$run/transitions.json"
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $capture = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    foreach ($state in @('Intro','EyeCalibration','Launch','Playing')) {
        $frames = @($capture.frames | Where-Object state -eq $state)
        if ($frames.Count -eq 0) { continue }
        $elapsed = 0.0
        $window = @($frames | ForEach-Object {
            if ($elapsed -lt 1000) { $_ }
            $elapsed += $_.mainThreadFrameMs
        })
        $peak = $window | Sort-Object playerLoopMs -Descending | Select-Object -First 1
        $allPeak = $frames | Sort-Object playerLoopMs -Descending | Select-Object -First 1
        [pscustomobject]@{
            Run=$run; State=$state; EntryFrame=$frames[0].index; WindowFrames=$window.Count
            PeakFrame=$peak.index; PeakPlayerLoopMs=[math]::Round($peak.playerLoopMs,3)
            PeakFullFrameMs=[math]::Round($peak.mainThreadFrameMs,3)
            WindowMaxFullFrameMs=[math]::Round(($window | Measure-Object mainThreadFrameMs -Maximum).Maximum,3)
            MainThreadGcBytesAtPeak=$peak.gcAllocBytes
            MaxGcBytesInWindow=($window | Measure-Object gcAllocBytes -Maximum).Maximum
            MaxAudioMs=[math]::Round(($window | Measure-Object audioMs -Maximum).Maximum,3)
            MaxInstantiateMs=[math]::Round(($window | Measure-Object instantiateMs -Maximum).Maximum,3)
            MaxGcCollectMs=[math]::Round(($window | Measure-Object gcCollectMs -Maximum).Maximum,3)
            WholeStatePeakMs=[math]::Round($allPeak.playerLoopMs,3); WholeStatePeakFrame=$allPeak.index
        }
    }
}
$rows | Export-Csv -LiteralPath (Join-Path $comparisonPath 'transition-comparison.csv') -NoTypeInformation -Encoding utf8
$rows | Format-Table Run,State,PeakPlayerLoopMs,PeakFullFrameMs,MaxAudioMs,MaxInstantiateMs,MaxGcBytesInWindow,WholeStatePeakMs -AutoSize
$startupRows = foreach ($run in $Runs) {
    $path = Join-Path $qaPath "$run/startup.json"
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $frames = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).frames
    $peak = $frames | Sort-Object playerLoopMs -Descending | Select-Object -First 1
    [pscustomobject]@{
        Run=$run; PeakFrame=$peak.index; PeakPlayerLoopMs=[math]::Round($peak.playerLoopMs,3)
        FullFrameAtCpuPeakMs=[math]::Round($peak.mainThreadFrameMs,3)
        MaxAudioMs=[math]::Round(($frames | Measure-Object audioMs -Maximum).Maximum,3)
        MaxShaderMs=[math]::Round(($frames | Measure-Object shaderMs -Maximum).Maximum,3)
        MaxGcCollectMs=[math]::Round(($frames | Measure-Object gcCollectMs -Maximum).Maximum,3)
        GcBytesAtPeak=$peak.gcAllocBytes
    }
}
$startupRows | Export-Csv -LiteralPath (Join-Path $comparisonPath 'startup-comparison.csv') -NoTypeInformation -Encoding utf8
$startupRows | Format-Table -AutoSize
# Inclusive marker totals overlap. They must not be added together as a CPU-time budget.
Write-Output 'Window: first 1000 ms of captured frame time after each destination state begins. CPU: PlayerLoop, not EditorLoop or frame-rate sleep. GC bytes include QA/editor allocations.'
