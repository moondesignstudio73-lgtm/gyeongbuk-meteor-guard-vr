param([string[]]$Runs = @('after-1','after-2','after-3','motion-1','motion-2','motion-3'))
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$capturePath = Join-Path $projectPath 'Docs/StartupHitchQA'
$outputPath = Join-Path $projectPath 'Docs/UiMotionQA'
$rows = foreach ($run in $Runs) {
    $file = Join-Path $capturePath "$run/transitions.json"
    if (-not (Test-Path -LiteralPath $file)) { continue }
    $capture = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    foreach ($state in @('Intro','EyeCalibration','Launch','Playing')) {
        $frames = @($capture.frames | Where-Object state -eq $state)
        $elapsed = 0.0
        $window = @($frames | ForEach-Object { if ($elapsed -lt 1000) { $_ }; $elapsed += $_.mainThreadFrameMs })
        if ($window.Count -eq 0) { continue }
        [pscustomobject]@{
            Run=$run; State=$state
            CpuPeakMs=[math]::Round(($window | Measure-Object playerLoopMs -Maximum).Maximum,3)
            FullFramePeakMs=[math]::Round(($window | Measure-Object mainThreadFrameMs -Maximum).Maximum,3)
            CanvasPeakMs=[math]::Round(($window | Measure-Object canvasMs -Maximum).Maximum,3)
            TmpPeakMs=[math]::Round(($window | Measure-Object tmpMs -Maximum).Maximum,3)
            GcCollectPeakMs=[math]::Round(($window | Measure-Object gcCollectMs -Maximum).Maximum,3)
            InstantiatePeakMs=[math]::Round(($window | Measure-Object instantiateMs -Maximum).Maximum,3)
            MaxMainThreadGcBytes=($window | Measure-Object gcAllocBytes -Maximum).Maximum
        }
    }
}
$rows | Export-Csv -LiteralPath (Join-Path $outputPath 'performance-comparison.csv') -NoTypeInformation -Encoding utf8
$rows | Format-Table -AutoSize
$markers = foreach ($run in $Runs) {
    $file = Join-Path $capturePath "$run/transitions-markers.csv"
    if (-not (Test-Path -LiteralPath $file)) { continue }
    $entries = @(Import-Csv -LiteralPath $file | Where-Object marker -eq 'MD.UI.Motion')
    if ($entries.Count -eq 0) { continue }
    $values = @($entries | ForEach-Object { [double]$_.inclusiveMs })
    [pscustomobject]@{
        Run=$run; Marker='MD.UI.Motion (director only)'; Frames=$entries.Count
        MeanMs=[math]::Round(($values | Measure-Object -Average).Average,4)
        PeakMs=[math]::Round(($values | Measure-Object -Maximum).Maximum,4)
    }
}
$markers | Export-Csv -LiteralPath (Join-Path $outputPath 'motion-marker.csv') -NoTypeInformation -Encoding utf8
$markers | Format-Table -AutoSize
Write-Output 'Editor CPU Profiler; first 1000ms destination-state window; QA/editor included. Canvas/TMP markers are inclusive and must not be summed. No Windows/HMD FPS inference.'
