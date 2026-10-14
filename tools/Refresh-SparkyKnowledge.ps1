[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$StagedBundlePath,

    [string]$ExporterPath = 'C:\Program Files\PerpetuumSparky\Perpetuum.ItemStatsExporter.exe',
    [string]$BundleCatalogDirectory = (Join-Path $env:ProgramData 'Sparky\bundles')
)

$ErrorActionPreference = 'Stop'

if (-not [IO.Path]::IsPathRooted($StagedBundlePath) -or -not [IO.Path]::IsPathRooted($ExporterPath)) {
    throw 'The staged bundle and exporter paths must be absolute.'
}
$StagedBundlePath = [IO.Path]::GetFullPath($StagedBundlePath)
$ExporterPath = [IO.Path]::GetFullPath($ExporterPath)
$BundleCatalogDirectory = [IO.Path]::GetFullPath($BundleCatalogDirectory)
$expectedCatalogDirectory = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'Sparky\bundles'))
if (-not $BundleCatalogDirectory.Equals($expectedCatalogDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "BundleCatalogDirectory must be the protected Sparky catalog at $expectedCatalogDirectory."
}
if (-not (Test-Path -LiteralPath $StagedBundlePath -PathType Leaf)) { throw 'Staged bundle not found.' }
if (-not (Test-Path -LiteralPath $ExporterPath -PathType Leaf)) { throw 'Sparky exporter not found.' }
if ((Get-Item -LiteralPath $StagedBundlePath -Force).Length -gt 100MB) { throw 'The staged bundle exceeds the 100 MB safety limit.' }
if (((Get-Item -LiteralPath $StagedBundlePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The staged bundle cannot be a symbolic link or junction.'
}

New-Item -ItemType Directory -Path $BundleCatalogDirectory -Force | Out-Null
if (((Get-Item -LiteralPath $BundleCatalogDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The protected bundle catalog cannot be a symbolic link or junction.'
}

$bundle = Get-Content -LiteralPath $StagedBundlePath -Raw -Encoding UTF8 | ConvertFrom-Json
$bundleVersion = [string]$bundle.Manifest.BundleVersion
if ($bundleVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') {
    throw 'The staged bundle has an invalid version identifier.'
}

$candidatePath = Join-Path $BundleCatalogDirectory ('.stage-' + [Guid]::NewGuid().ToString('N') + '.json')
$destinationPath = Join-Path $BundleCatalogDirectory ($bundleVersion + '.json')
try {
    Copy-Item -LiteralPath $StagedBundlePath -Destination $candidatePath
    $validationOutput = & $ExporterPath --validate-bundle $candidatePath 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Sparky rejected the staged knowledge bundle: $($validationOutput.Trim())"
    }

    if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
        $existingHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
        $candidateHash = (Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash
        if (-not [string]::Equals($existingHash, $candidateHash, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A different bundle already exists with this immutable version identifier.'
        }
        Remove-Item -LiteralPath $candidatePath -Force
    }
    else {
        [IO.File]::Move($candidatePath, $destinationPath)
    }

    [PSCustomObject]@{
        Status = 'Staged'
        BundleVersion = $bundleVersion
        BundlePath = $destinationPath
        Validation = $validationOutput.Trim()
        Activation = 'Select and activate this version for Dev in the Sparky Admin Panel.'
    }
}
finally {
    if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
        Remove-Item -LiteralPath $candidatePath -Force
    }
}
