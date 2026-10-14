[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$StagedBundlePath,

    [string]$ExporterPath = 'C:\Program Files\PerpetuumSparky\Perpetuum.ItemStatsExporter.exe',
    [string]$ActiveBundlePath = 'C:\ProgramData\Sparky\output.json',
    [string]$ServiceName = 'Perpetuum.Sparky',
    [string]$HealthUri = 'https://127.0.0.1:5080/health',
    [string]$CertificatePath = 'C:\ProgramData\Sparky\sparky-api.cer',
    [ValidateRange(10, 300)]
    [int]$HealthTimeoutSeconds = 90,
    [ValidateRange(1, 100)]
    [int]$BackupsToKeep = 10
)

$ErrorActionPreference = 'Stop'

function Invoke-SparkyHealth {
    param([string]$ExpectedBundleVersion, [int]$TimeoutSeconds)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastCheck = 'No health response received.'
    do {
        try {
            $health = Invoke-RestMethod -Uri $HealthUri -Method Get -TimeoutSec 5
            if ($health.status -ne 'ok') {
                $lastCheck = "Health returned status '$($health.status)'."
            }
            elseif (-not $ExpectedBundleVersion -or
                $health.bundleVersion -eq $ExpectedBundleVersion.Substring(0, [Math]::Min(16, $ExpectedBundleVersion.Length))) {
                return $health
            }
            else {
                $lastCheck = "Health reported bundle version '$($health.bundleVersion)'."
            }
        }
        catch {
            $lastCheck = $_.Exception.Message
            Start-Sleep -Seconds 2
        }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Sparky HTTPS health did not report the expected bundle version within $TimeoutSeconds seconds. Last check: $lastCheck"
}

function New-SparkyCertificatePinCallback {
    param([Parameter(Mandatory = $true)][string]$Thumbprint)

    if (-not ('SparkyCertificatePinValidator' -as [type])) {
        Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class SparkyCertificatePinValidator
{
    private static string expectedThumbprint;

    public static RemoteCertificateValidationCallback Create(string thumbprint)
    {
        if (String.IsNullOrWhiteSpace(thumbprint))
            throw new ArgumentException("A certificate thumbprint is required.", "thumbprint");

        expectedThumbprint = Normalize(thumbprint);
        return new RemoteCertificateValidationCallback(Validate);
    }

    private static bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
    {
        HttpWebRequest request = sender as HttpWebRequest;
        if (request == null || certificate == null)
            return false;

        Uri uri = request.RequestUri;
        if (uri == null || uri.Scheme != Uri.UriSchemeHttps ||
            !String.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            !String.Equals(uri.AbsolutePath, "/health", StringComparison.Ordinal) ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            return false;

        using (X509Certificate2 presented = new X509Certificate2(certificate))
            return String.Equals(Normalize(presented.Thumbprint), expectedThumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string thumbprint)
    {
        return thumbprint.Replace(" ", String.Empty).Replace(":", String.Empty);
    }
}
'@
    }

    return [SparkyCertificatePinValidator]::Create($Thumbprint)
}

function Invoke-SparkyPinnedHealth {
    param(
        [string]$ExpectedBundleVersion,
        [int]$TimeoutSeconds,
        [System.Net.Security.RemoteCertificateValidationCallback]$CertificateCallback
    )

    $previousValidationCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $CertificateCallback
        return Invoke-SparkyHealth -ExpectedBundleVersion $ExpectedBundleVersion -TimeoutSeconds $TimeoutSeconds
    }
    finally {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousValidationCallback
    }
}

if (-not [IO.Path]::IsPathRooted($StagedBundlePath) -or
    -not [IO.Path]::IsPathRooted($ExporterPath) -or
    -not [IO.Path]::IsPathRooted($ActiveBundlePath) -or
    -not [IO.Path]::IsPathRooted($CertificatePath)) {
    throw 'Staged bundle, exporter, active bundle, and certificate paths must be absolute.'
}

$StagedBundlePath = [IO.Path]::GetFullPath($StagedBundlePath)
$ExporterPath = [IO.Path]::GetFullPath($ExporterPath)
$ActiveBundlePath = [IO.Path]::GetFullPath($ActiveBundlePath)
$CertificatePath = [IO.Path]::GetFullPath($CertificatePath)

if (-not (Test-Path -LiteralPath $StagedBundlePath -PathType Leaf)) { throw "Staged bundle not found: $StagedBundlePath" }
if (-not (Test-Path -LiteralPath $ExporterPath -PathType Leaf)) { throw "Sparky exporter not found: $ExporterPath" }
if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) { throw "Sparky HTTPS certificate not found: $CertificatePath" }
if ((Get-Item -LiteralPath $StagedBundlePath).Length -gt 100MB) { throw 'The staged bundle exceeds the 100 MB safety limit.' }
if (-not $HealthUri.StartsWith('https://127.0.0.1:', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'HealthUri must use HTTPS on IPv4 loopback (127.0.0.1).'
}
$expectedThumbprint = (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)).Thumbprint -replace '\s', ''
$certificateValidationCallback = New-SparkyCertificatePinCallback -Thumbprint $expectedThumbprint

$activeDirectory = [IO.Path]::GetDirectoryName($ActiveBundlePath)
if (-not (Test-Path -LiteralPath $activeDirectory -PathType Container)) { throw "Active bundle directory not found: $activeDirectory" }
$backupDirectory = Join-Path $activeDirectory 'knowledge-backups'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

$candidatePath = Join-Path $activeDirectory ('.sparky-knowledge-' + [Guid]::NewGuid().ToString('N') + '.staged')
$bundle = Get-Content -LiteralPath $StagedBundlePath -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedBundleVersion = [string]$bundle.Manifest.BundleVersion
if ([string]::IsNullOrWhiteSpace($expectedBundleVersion)) { throw 'The staged bundle has no manifest bundle version.' }

Copy-Item -LiteralPath $StagedBundlePath -Destination $candidatePath
try {
    $validationOutput = & $ExporterPath --validate-bundle $candidatePath 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Sparky rejected the staged knowledge bundle: $validationOutput"
    }

    $service = Get-Service -Name $ServiceName
    $wasRunning = $service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running
    $oldBundleVersion = $null
    if (Test-Path -LiteralPath $ActiveBundlePath -PathType Leaf) {
        try {
            $oldBundle = Get-Content -LiteralPath $ActiveBundlePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $oldBundleVersion = [string]$oldBundle.Manifest.BundleVersion
        }
        catch {
            $oldBundleVersion = $null
        }
    }

    if ($wasRunning) {
        Stop-Service -Name $ServiceName
        (Get-Service -Name $ServiceName).WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(45))
    }

    $backupPath = Join-Path $backupDirectory ('output-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
    $replaced = $false
    try {
        if (Test-Path -LiteralPath $ActiveBundlePath -PathType Leaf) {
            [IO.File]::Replace($candidatePath, $ActiveBundlePath, $backupPath, $true)
        }
        else {
            [IO.File]::Move($candidatePath, $ActiveBundlePath)
        }
        $replaced = $true

        Start-Service -Name $ServiceName
        (Get-Service -Name $ServiceName).WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(45))

        $health = Invoke-SparkyPinnedHealth `
            -ExpectedBundleVersion $expectedBundleVersion `
            -TimeoutSeconds $HealthTimeoutSeconds `
            -CertificateCallback $certificateValidationCallback

        Get-ChildItem -LiteralPath $backupDirectory -Filter 'output-*.json' -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -Skip $BackupsToKeep |
            Remove-Item -Force

        [PSCustomObject]@{
            Status = 'Activated'
            BundleVersion = $health.bundleVersion
            BackupPath = if (Test-Path -LiteralPath $backupPath) { $backupPath } else { $null }
            Validation = $validationOutput.Trim()
        }
    }
    catch {
        $activationError = $_
        if ($replaced -and (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
            try {
                $service = Get-Service -Name $ServiceName
                if ($service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) {
                    Stop-Service -Name $ServiceName
                    (Get-Service -Name $ServiceName).WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(45))
                }
                Copy-Item -LiteralPath $backupPath -Destination $ActiveBundlePath -Force
                Start-Service -Name $ServiceName
                (Get-Service -Name $ServiceName).WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(45))

                if ($oldBundleVersion) {
                    $null = Invoke-SparkyPinnedHealth `
                        -ExpectedBundleVersion $oldBundleVersion `
                        -TimeoutSeconds $HealthTimeoutSeconds `
                        -CertificateCallback $certificateValidationCallback
                }
                else {
                    $null = Invoke-SparkyPinnedHealth `
                        -ExpectedBundleVersion '' `
                        -TimeoutSeconds $HealthTimeoutSeconds `
                        -CertificateCallback $certificateValidationCallback
                }
            }
            catch {
                throw "Bundle activation failed ($activationError). Rollback also failed ($($_.Exception.Message)); active bundle backup is '$backupPath'."
            }
        }

        throw "Bundle activation failed; previous bundle was restored when available. $($activationError.Exception.Message)"
    }
}
finally {
    if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
        Remove-Item -LiteralPath $candidatePath -Force
    }
}
