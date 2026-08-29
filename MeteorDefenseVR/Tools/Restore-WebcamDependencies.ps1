$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$cacheDirectory = Join-Path $projectDirectory 'DependencyCache'
$archivePath = Join-Path $cacheDirectory 'com.github.homuler.mediapipe-0.16.3.tgz'
$expectedHash = 'CC3E77A219E0B99618AE3BE64C31A566197DEEDC69C1E136ACF52D65D7CF2E79'
New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null
if (-not (Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest 'https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v0.16.3/com.github.homuler.mediapipe-0.16.3.tgz' -OutFile $archivePath
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'MediaPipe package checksum mismatch. Do not open Unity with this archive.'
}
Write-Output 'MediaPipe 0.16.3 verified. Open the Unity project; no runtime downloads are needed.'
