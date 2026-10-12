param([string]$CorrectedArchive = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$checker = Join-Path $repo 'Test-ReleaseParts.ps1'
$sandbox = Join-Path $PSScriptRoot ('obj/fixture-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($sandbox) | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'Distribution/QuantumRelay/Parts') -Destination (Join-Path $sandbox 'Parts') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'Distribution/QuantumRelay/Models') -Destination (Join-Path $sandbox 'Models') -Recurse
$count = 0
function Reject([string]$label, [scriptblock]$action, [string]$reason) {
    $rejected = $false
    try { & $action | Out-Null }
    catch { if ($_.Exception.Message -notmatch $reason) { throw }; $rejected = $true }
    if (!$rejected) { throw "Bad input was accepted: $label" }
    Write-Output "PASS $label"
    $script:count++
}
& $checker -GameDataDirectory $sandbox
$count++
$part = Join-Path $sandbox 'Parts/QR-100_Pioneer.cfg'
$original = [IO.File]::ReadAllText($part)
try {
    [IO.File]::WriteAllText($part, '+PART[nfex-antenna-reflector-side-3] { @name = quantum-relay-qr-100 }')
    Reject 'legacy NFE patch rejected' { & $checker -GameDataDirectory $sandbox } 'legacy patch detected'
    [IO.File]::WriteAllText($part, $original.Replace('entryCost = 42000', 'entryCost = 1'))
    Reject 'altered standalone config rejected' { & $checker -GameDataDirectory $sandbox } 'differs from canonical'
} finally { [IO.File]::WriteAllText($part, $original) }
$model = Join-Path $sandbox 'Models/RW_QR100.mu'
$modelBackup = $model + '.fixture-backup'
Move-Item -LiteralPath $model -Destination $modelBackup
try { Reject 'missing standalone model rejected' { & $checker -GameDataDirectory $sandbox } 'model missing or changed' }
finally { Move-Item -LiteralPath $modelBackup -Destination $model }
$oldArchive = Join-Path $repo 'artifacts/Quantum-Relay-v1.03-GameData.zip'
if (Test-Path -LiteralPath $oldArchive) {
    Reject 'original v1.03 player ZIP rejected' { & $checker -ArchivePath $oldArchive } 'differs from canonical'
}
if ($CorrectedArchive) { & $checker -ArchivePath $CorrectedArchive; $count++ }
Write-Output "$count packaging regression checks passed. Fixtures remain under ignored Tests/Packaging/obj."
