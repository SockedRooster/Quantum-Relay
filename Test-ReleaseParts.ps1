param(
    [string]$GameDataDirectory = (Join-Path $PSScriptRoot 'GameData/QuantumRelay'),
    [string]$ArchivePath = ''
)
$ErrorActionPreference = 'Stop'
$canonical = Join-Path $PSScriptRoot 'Distribution/QuantumRelay'
$parts = @(Get-ChildItem -LiteralPath (Join-Path $canonical 'Parts') -Filter '*.cfg' -File)
if ($ArchivePath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
    try {
        $partEntries = @($archive.Entries | Where-Object FullName -match '^GameData/QuantumRelay/Parts/[^/]+\.cfg$')
        if ($partEntries.Count -ne 4) { throw 'Expected four standalone part configs in player archive.' }
        $canonicalFiles = @($parts) + @(Get-ChildItem -LiteralPath (Join-Path $canonical 'Models') -Filter 'RW_QR*.mu' -File)
        foreach ($file in $canonicalFiles) {
            $relative = $file.FullName.Substring($canonical.Length + 1).Replace('\', '/')
            $entry = $archive.GetEntry('GameData/QuantumRelay/' + $relative)
            if ($null -eq $entry) { throw "Missing canonical asset in player archive: $relative" }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
                throw "Archived asset differs from canonical Distribution asset: $relative"
            }
        }
    } finally { $archive.Dispose() }
    Write-Output 'PASS: archived standalone part configs and models match canonical Distribution assets.'
    return
}
$stagedParts = @(Get-ChildItem -LiteralPath (Join-Path $GameDataDirectory 'Parts') -Filter '*.cfg' -File)
if ($parts.Count -ne 4 -or $stagedParts.Count -ne 4) { throw 'Expected exactly four standalone QR part configs.' }
$ids = @{}
foreach ($part in $parts) {
    $staged = Join-Path $GameDataDirectory ('Parts/' + $part.Name)
    $text = Get-Content -LiteralPath $staged -Raw
    if ($text -notmatch '(?m)^\s*PART\s*\r?\n?\s*\{' -or $text -match '(?m)^\s*\+PART\[') {
        throw "Standalone PART root missing (legacy patch detected): $($part.Name)"
    }
    if ((Get-FileHash -LiteralPath $staged).Hash -ne (Get-FileHash -LiteralPath $part.FullName).Hash) {
        throw "Staged part differs from canonical Distribution config: $($part.Name)"
    }
    $id = [regex]::Match($text, '(?m)^\s*name\s*=\s*(quantum-relay-qr-(?:100|250|500|750))\s*$')
    if (!$id.Success -or $ids.ContainsKey($id.Groups[1].Value)) { throw "Missing/duplicate QR part ID: $($part.Name)" }
    $ids[$id.Groups[1].Value] = $true
    $model = [regex]::Match($text, '(?m)^\s*model\s*=\s*(QuantumRelay/Models/RW_QR(?:100|250|500|750))\s*$')
    if (!$model.Success) { throw "Standalone RoosterWorks model reference missing: $($part.Name)" }
    $relativeModel = $model.Groups[1].Value.Substring('QuantumRelay/'.Length) + '.mu'
    $stagedModel = Join-Path $GameDataDirectory $relativeModel
    $canonicalModel = Join-Path $canonical $relativeModel
    if (!(Test-Path -LiteralPath $stagedModel) -or
        (Get-FileHash -LiteralPath $stagedModel).Hash -ne (Get-FileHash -LiteralPath $canonicalModel).Hash) {
        throw "Standalone model missing or changed: $relativeModel"
    }
}
Write-Output 'PASS: all four standalone parts and their models match canonical Distribution assets.'
