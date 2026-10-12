param(
    [string]$KSPRoot = 'D:\SteamLibrary\steamapps\common\Kerbal Space Program',
    [string]$OutputDirectory = '',
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$buildArguments = @('build', (Join-Path $repoRoot 'QuantumRelay/QuantumRelay.csproj'),
    '--configuration', 'Release', "-p:KSPRoot=$KSPRoot")
if ($NoRestore) { $buildArguments += '--no-restore' }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Release compilation failed.' }

$versionFile = Join-Path $repoRoot 'Distribution/QuantumRelay/QuantumRelay.version'
$metadata = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
if ($metadata.VERSION.MAJOR -ne 1 -or $metadata.VERSION.MINOR -ne 3 -or $metadata.VERSION.PATCH -ne 1) {
    throw 'Expected v1.03.1 numeric metadata (1.3.1).'
}
$publicVersion = 'v1.03.1'
$dll = Join-Path $repoRoot 'GameData/QuantumRelay/Plugins/QuantumRelay.dll'
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll)
if ($dllVersion.FileVersion -ne '1.3.1.0' -or $dllVersion.ProductVersion -ne '1.03.1') {
    throw 'Release DLL version does not match the package.'
}
& (Join-Path $repoRoot 'Test-ReleaseParts.ps1')

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-VerifiedZip([string]$zipPath, $entries) {
    # Create refuses to overwrite an existing release archive.
    $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $entries) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.File,
                $entry.Name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($archive.Entries.Count -ne $entries.Count) { throw 'Archive entry count mismatch.' }
        $inputs = @{}
        foreach ($entry in $entries) { $inputs[$entry.Name] = $entry.File }
        foreach ($entry in $archive.Entries) {
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $inputs[$entry.FullName] -Algorithm SHA256).Hash) {
                throw "Archive content mismatch: $($entry.FullName)"
            }
            if ($entry.FullName -match '/GameData/QuantumRelay/Parts/([^/]+\.cfg)$' -or
                $entry.FullName -match '^GameData/QuantumRelay/Parts/([^/]+\.cfg)$') {
                $partName = $Matches[1]
                $canonicalPart = Join-Path $repoRoot ('Distribution/QuantumRelay/Parts/' + $partName)
                if ($hash -ne (Get-FileHash -LiteralPath $canonicalPart -Algorithm SHA256).Hash) {
                    throw "Packaged part differs from canonical Distribution config: $partName"
                }
            }
        }
    } finally { $archive.Dispose() }
    Write-Output "Verified $($entries.Count) entries: $zipPath"
}

$gameData = Join-Path $repoRoot 'GameData/QuantumRelay'
$playerEntries = @(
    foreach ($file in Get-ChildItem -LiteralPath $gameData -Recurse -File) {
        $relative = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        if ($relative -match '\.(pdb|mdb)$' -or $relative -match '/PluginData/Settings\.cfg$' -or
            $relative -match '/PluginData/.*Telemetry.*' -or $relative -match '\.log$') { continue }
        [pscustomobject]@{ File = $file.FullName; Name = $relative }
    }
)
$playerZip = Join-Path $OutputDirectory "Quantum-Relay-$publicVersion-GameData.zip"
Write-VerifiedZip $playerZip $playerEntries

$trackedPaths = & git -C $repoRoot ls-files
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source repository files.' }
$sourcePrefix = "Quantum-Relay-$publicVersion-Source/"
$sourceEntries = @(
    foreach ($relative in $trackedPaths) {
        [pscustomobject]@{ File = (Join-Path $repoRoot $relative); Name = $sourcePrefix + $relative }
    }
    foreach ($entry in $playerEntries) {
        [pscustomobject]@{ File = $entry.File; Name = $sourcePrefix + $entry.Name }
    }
)
$sourceZip = Join-Path $OutputDirectory "Quantum-Relay-$publicVersion-Source.zip"
Write-VerifiedZip $sourceZip $sourceEntries
$hashLines = foreach ($path in @($playerZip, $sourceZip)) {
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path)
}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'), $hashLines)
Write-Output 'Player ZIP, source ZIP, and SHA256SUMS.txt are ready.'
