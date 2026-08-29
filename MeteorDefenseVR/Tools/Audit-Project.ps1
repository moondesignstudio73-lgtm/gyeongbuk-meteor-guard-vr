$ErrorActionPreference = 'Stop'
$auditRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$auditOutput = Join-Path $auditRoot 'Docs\AutonomousQA'
[void](New-Item -ItemType Directory -Path $auditOutput -Force)
$auditFiles = @(Get-ChildItem -LiteralPath (Join-Path $auditRoot 'Assets'),(Join-Path $auditRoot 'Packages'),(Join-Path $auditRoot 'ProjectSettings'),(Join-Path $auditRoot 'Tools') -File -Recurse)
$auditTextExtensions = @('.cs','.json','.asset','.unity','.prefab','.shader','.hlsl','.ps1','.cmd','.md','.txt')
$auditFindings = [System.Collections.Generic.List[object]]::new()
$auditUrls = [System.Collections.Generic.HashSet[string]]::new()
$auditUpdateCandidates = [System.Collections.Generic.List[object]]::new()
# Values are deliberately never included in output, even for a suspected credential.
$auditSecretPatterns = @('-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----','\b(?:sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|AKIA[A-Z0-9]{16})\b','(?i)(api[_-]?key|secret|password|credential|webhook|token)\s*[:=]\s*["''][^"''\r\n]{12,}["'']')
foreach ($auditFile in $auditFiles) {
    if ($auditFile.Extension -notin $auditTextExtensions) { continue }
    $auditRelative = $auditFile.FullName.Substring($auditRoot.Length + 1)
    $auditText = [IO.File]::ReadAllText($auditFile.FullName)
    foreach ($auditPattern in $auditSecretPatterns) {
        foreach ($auditMatch in [regex]::Matches($auditText,$auditPattern)) {
            $auditLine = 1 + [regex]::Matches($auditText.Substring(0,$auditMatch.Index),'\n').Count
            $auditFindings.Add([pscustomobject]@{file=$auditRelative;line=$auditLine;classification='REVIEW_LITERAL_REDACTED'})
        }
    }
    foreach ($auditMatch in [regex]::Matches($auditText,'https?://([A-Za-z0-9.-]+)')) { [void]$auditUrls.Add($auditMatch.Groups[1].Value) }
    if ($auditFile.Extension -eq '.cs' -and $auditRelative -notmatch '\\(Editor|Tests)\\') {
        if ($auditText -match '\b(?:LateUpdate|Update|FixedUpdate)\s*\(') {
            $auditOps = @([regex]::Matches($auditText,'\b(?:Find\w*|GetComponent\w*|Instantiate|Destroy|OrderBy|Where|Select|ToArray)(?:<[^>\r\n]+>)?\s*\(|Camera\.main|new (?:List|Dictionary)') | ForEach-Object { $_.Value.Trim() } | Sort-Object -Unique)
            $auditUpdateCandidates.Add([pscustomobject]@{file=$auditRelative;candidateOperations=$auditOps;scope='Whole-file prefilter; manual call-path review required, not proof of per-frame use'})
        }
    }
}
$auditAssets = @($auditFiles | Where-Object { $_.FullName.StartsWith((Join-Path $auditRoot 'Assets')) -and $_.Extension -ne '.meta' })
$auditDuplicates = @($auditAssets | Where-Object { $_.Extension -in @('.png','.wav','.mp3') } | ForEach-Object { [pscustomobject]@{file=$_.FullName.Substring($auditRoot.Length+1);hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} } | Group-Object hash | Where-Object Count -gt 1 | ForEach-Object { @($_.Group.file) })
$auditReport = [ordered]@{
    generatedUtc=[datetime]::UtcNow.ToString('O'); filesScanned=$auditFiles.Count
    scope='Assets, Packages manifest/lock, ProjectSettings and Tools; not Unity Library/cache, git history or binary native-plugin decompilation'
    suspectedSecretLiterals=@($auditFindings.ToArray()); urlDomains=@($auditUrls | Sort-Object)
    updateReviewCandidates=@($auditUpdateCandidates.ToArray())
    assetCounts=@($auditAssets | Group-Object Extension | Select-Object Name,Count)
    largestAssets=@($auditAssets | Sort-Object Length -Descending | Select-Object -First 15 @{Name='file';Expression={$_.FullName.Substring($auditRoot.Length+1)}},Length)
    duplicateMediaHashes=$auditDuplicates
}
$auditReport | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $auditOutput 'static-audit.json') -Encoding utf8
Write-Output ('Audit files=' + $auditFiles.Count + '; redacted credential candidates=' + $auditFindings.Count + '; Update-bearing scripts=' + $auditUpdateCandidates.Count)
