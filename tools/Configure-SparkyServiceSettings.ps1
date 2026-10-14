param(
    [Parameter(Mandatory = $true)]
    [string]$SettingsPath,

    [Parameter(Mandatory = $true)]
    [string]$ModelId,

    [Parameter(Mandatory = $true)]
    [uri]$ModelBaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$ServiceCertificatePath,

    [Parameter(Mandatory = $true)]
    [string]$SshHost,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$SshPort,

    [Parameter(Mandatory = $true)]
    [string]$SshUser,

    [Parameter(Mandatory = $true)]
    [string]$SshPrivateKeyPath,

    [Parameter(Mandatory = $true)]
    [string]$SshKnownHostsPath,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$VdmForwardPort,

    [string]$AdminSshHost,

    [ValidateRange(1, 65535)]
    [int]$AdminSshPort = 22,

    [string]$AdminSshUser,

    [string]$AdminSshPrivateKeyPath,

    [string]$AdminSshKnownHostsPath,

    [ValidateRange(1, 65535)]
    [int]$AdminForwardPort = 5082
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    # Windows PowerShell 5.1 exposes ProtectedData through System.Security.
    Add-Type -AssemblyName System.Security
}
else {
    # PowerShell 7+ ships the API in its own assembly.
    Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
}

function ConvertTo-DpapiValue {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$Secret
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secret)
    $bytes = $null
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $bytes = [Text.Encoding]::UTF8.GetBytes($plain)
        $protected = [Security.Cryptography.ProtectedData]::Protect(
            $bytes,
            $null,
            [Security.Cryptography.DataProtectionScope]::LocalMachine)
        return 'dpapi:' + [Convert]::ToBase64String($protected)
    }
    finally {
        if ($null -ne $bytes) {
            [Array]::Clear($bytes, 0, $bytes.Length)
        }
        if ($null -ne $bstr -and $bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
        $plain = $null
    }
}

if (-not (Test-Path -LiteralPath $ServiceCertificatePath -PathType Leaf)) {
    throw 'The configured PFX certificate file does not exist.'
}
if (-not (Test-Path -LiteralPath $SshPrivateKeyPath -PathType Leaf)) {
    throw 'The configured SSH private key file does not exist.'
}
if (-not (Test-Path -LiteralPath $SshKnownHostsPath -PathType Leaf)) {
    throw 'The configured known-hosts pin file does not exist.'
}
$adminTunnelValues = @($AdminSshHost, $AdminSshUser, $AdminSshPrivateKeyPath, $AdminSshKnownHostsPath)
$adminTunnelProvided = @($adminTunnelValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
if ($adminTunnelProvided -ne 0 -and $adminTunnelProvided -ne $adminTunnelValues.Count) {
    throw 'Configure all four admin tunnel values (host, user, private key, known-hosts file) or leave them all empty.'
}
$adminTunnelEnabled = $adminTunnelProvided -eq $adminTunnelValues.Count
if ($adminTunnelEnabled) {
    if (-not (Test-Path -LiteralPath $AdminSshPrivateKeyPath -PathType Leaf)) {
        throw 'The configured admin SSH private key does not exist.'
    }
    if (-not (Test-Path -LiteralPath $AdminSshKnownHostsPath -PathType Leaf)) {
        throw 'The configured admin known-hosts pin file does not exist.'
    }
    $gameTunnelFiles = @([IO.Path]::GetFullPath($SshPrivateKeyPath), [IO.Path]::GetFullPath($SshKnownHostsPath))
    $adminTunnelFiles = @([IO.Path]::GetFullPath($AdminSshPrivateKeyPath), [IO.Path]::GetFullPath($AdminSshKnownHostsPath))
    if (@($adminTunnelFiles | Where-Object { $gameTunnelFiles -contains $_ }).Count -gt 0 -or
        $adminTunnelFiles[0].Equals($adminTunnelFiles[1], [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The admin SSH key and known-hosts pin must use separate files from the game API tunnel.'
    }
    if ($AdminForwardPort -eq $VdmForwardPort) {
        throw 'The admin and game API tunnels must use different loopback listen ports on Main.'
    }
    if ($AdminSshUser -notmatch '^[A-Za-z0-9_.-]+$' -or $AdminSshHost.StartsWith('-')) {
        throw 'The admin SSH host or user contains unsupported characters.'
    }
}
if ($ModelBaseUrl.Scheme -notin @('http', 'https') -or
    $ModelBaseUrl.UserInfo.Length -gt 0 -or
    $ModelBaseUrl.Query.Length -gt 0 -or
    $ModelBaseUrl.Fragment.Length -gt 0 -or
    ($ModelBaseUrl.Scheme -eq 'http' -and $ModelBaseUrl.Host -notin @('localhost', '127.0.0.1', '::1'))) {
    throw 'The LM Studio URL must not contain credentials, a query, or a fragment; non-loopback URLs must use HTTPS.'
}

$apiToken = Read-Host 'Sparky Dev API bearer token (at least 32 characters)' -AsSecureString
if ($apiToken.Length -lt 32) {
    throw 'The Sparky Dev API bearer token must contain at least 32 characters.'
}
$liveApiToken = Read-Host 'Sparky Live API bearer token (leave empty to keep Live disabled)' -AsSecureString
if ($liveApiToken.Length -gt 0 -and $liveApiToken.Length -lt 32) {
    throw 'The Sparky Live API bearer token must contain at least 32 characters.'
}
$adminApiToken = Read-Host 'Sparky Admin bearer token (at least 32 characters; different from API token)' -AsSecureString
if ($adminApiToken.Length -lt 32) {
    throw 'The Sparky Admin API token must contain at least 32 characters.'
}
function ConvertFrom-SecureStringValue {
    param([Parameter(Mandatory = $true)][Security.SecureString]$Secret)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secret)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}
$devClearToken = ConvertFrom-SecureStringValue $apiToken
$adminClearToken = ConvertFrom-SecureStringValue $adminApiToken
$liveClearToken = if ($liveApiToken.Length -gt 0) { ConvertFrom-SecureStringValue $liveApiToken } else { $null }
if ([string]::Equals($adminClearToken, $devClearToken, [StringComparison]::Ordinal) -or
    ($null -ne $liveClearToken -and [string]::Equals($adminClearToken, $liveClearToken, [StringComparison]::Ordinal))) {
    throw 'The Sparky Admin API token must be different from both game API tokens.'
}
if ($null -ne $liveClearToken -and [string]::Equals($devClearToken, $liveClearToken, [StringComparison]::Ordinal)) {
    throw 'The Sparky Dev and Live API tokens must be different.'
}
$devClearToken = $null
$adminClearToken = $null
$liveClearToken = $null

$lmStudioToken = Read-Host 'LM Studio API token (leave empty if unused)' -AsSecureString
$pfxPassword = Read-Host 'PFX password (leave empty if the PFX has no password)' -AsSecureString

$settings = [ordered]@{
    SPARKY_DEV_API_TOKEN = ConvertTo-DpapiValue $apiToken
    SPARKY_ADMIN_API_TOKEN = ConvertTo-DpapiValue $adminApiToken
    SPARKY_LMSTUDIO_MODEL_ID = $ModelId
    SPARKY_LMSTUDIO_BASE_URL = $ModelBaseUrl.AbsoluteUri.TrimEnd('/')
    SPARKY_SERVICE_URL = 'https://127.0.0.1:5080'
    SPARKY_ADMIN_SERVICE_URL = 'https://127.0.0.1:5081'
    SPARKY_SERVICE_CERT_PATH = [IO.Path]::GetFullPath($ServiceCertificatePath)
    SPARKY_TUNNEL_ENABLED = 'true'
    SPARKY_SSH_HOST = $SshHost
    SPARKY_SSH_PORT = $SshPort.ToString([Globalization.CultureInfo]::InvariantCulture)
    SPARKY_SSH_USER = $SshUser
    SPARKY_SSH_PRIVATE_KEY_PATH = [IO.Path]::GetFullPath($SshPrivateKeyPath)
    SPARKY_SSH_KNOWN_HOSTS_PATH = [IO.Path]::GetFullPath($SshKnownHostsPath)
    SPARKY_VDM_FORWARD_PORT = $VdmForwardPort.ToString([Globalization.CultureInfo]::InvariantCulture)
    SPARKY_ADMIN_TUNNEL_ENABLED = $adminTunnelEnabled.ToString().ToLowerInvariant()
}

if ($liveApiToken.Length -gt 0) {
    $settings.SPARKY_LIVE_API_TOKEN = ConvertTo-DpapiValue $liveApiToken
}

if ($adminTunnelEnabled) {
    $settings.SPARKY_ADMIN_SSH_HOST = $AdminSshHost
    $settings.SPARKY_ADMIN_SSH_PORT = $AdminSshPort.ToString([Globalization.CultureInfo]::InvariantCulture)
    $settings.SPARKY_ADMIN_SSH_USER = $AdminSshUser
    $settings.SPARKY_ADMIN_SSH_PRIVATE_KEY_PATH = [IO.Path]::GetFullPath($AdminSshPrivateKeyPath)
    $settings.SPARKY_ADMIN_SSH_KNOWN_HOSTS_PATH = [IO.Path]::GetFullPath($AdminSshKnownHostsPath)
    $settings.SPARKY_ADMIN_FORWARD_PORT = $AdminForwardPort.ToString([Globalization.CultureInfo]::InvariantCulture)
}

if ($lmStudioToken.Length -gt 0) {
    $settings.SPARKY_LMSTUDIO_API_TOKEN = ConvertTo-DpapiValue $lmStudioToken
}
if ($pfxPassword.Length -gt 0) {
    $settings.SPARKY_SERVICE_CERT_PASSWORD = ConvertTo-DpapiValue $pfxPassword
}

$fullSettingsPath = [IO.Path]::GetFullPath($SettingsPath)
if ($fullSettingsPath.StartsWith('\\\\', [StringComparison]::Ordinal)) {
    throw 'SettingsPath must use a local filesystem path, not a network share.'
}
$settingsDirectory = [IO.Path]::GetDirectoryName($fullSettingsPath)
if ([IO.Path]::GetFileName($settingsDirectory) -ne 'Sparky') {
    throw 'SettingsPath must be inside a dedicated directory named Sparky.'
}

$normalizedDirectory = $settingsDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$restrictedRoots = @(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::Windows),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
foreach ($restrictedRoot in $restrictedRoots) {
    $normalizedRoot = [IO.Path]::GetFullPath($restrictedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($normalizedDirectory.Equals($normalizedRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedDirectory.StartsWith($normalizedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SettingsPath must not be inside a Windows or Program Files directory.'
    }
}
$normalizedProgramData = [IO.Path]::GetFullPath($env:ProgramData).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if ($normalizedDirectory.Equals($normalizedProgramData, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'SettingsPath must use a dedicated Sparky directory below ProgramData, not the ProgramData root.'
}

$ancestorPath = $settingsDirectory
while (-not [string]::IsNullOrWhiteSpace($ancestorPath)) {
    if (Test-Path -LiteralPath $ancestorPath) {
        $ancestorItem = Get-Item -LiteralPath $ancestorPath -Force
        if (($ancestorItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'SettingsPath ancestors must not use symbolic links or junctions.'
        }
    }

    $parentPath = [IO.Path]::GetDirectoryName($ancestorPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    if ([string]::IsNullOrWhiteSpace($parentPath) -or $parentPath -eq $ancestorPath) {
        break
    }

    $ancestorPath = $parentPath
}

New-Item -ItemType Directory -Force -Path $settingsDirectory | Out-Null
$settingsDirectoryItem = Get-Item -LiteralPath $settingsDirectory -Force
if (($settingsDirectoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'SettingsPath must not use a symbolic link or junction for its Sparky directory.'
}

$reparseItems = Get-ChildItem -LiteralPath $settingsDirectory -Force -Recurse -ErrorAction Stop |
    Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
if ($reparseItems) {
    throw 'The Sparky data directory must not contain symbolic links or junctions.'
}
& icacls.exe $settingsDirectory /inheritance:r /grant:r `
    '*S-1-5-19:(OI)(CI)RX' `
    '*S-1-5-32-544:(OI)(CI)F' `
    '*S-1-5-18:(OI)(CI)F' /T /C | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not restrict the Sparky data directory permissions.'
}

# The service needs write access only to the trace database and immutable bundle catalog.
foreach ($dataSubdirectoryName in @('traces', 'bundles')) {
    $dataSubdirectory = Join-Path $settingsDirectory $dataSubdirectoryName
    New-Item -ItemType Directory -Force -Path $dataSubdirectory | Out-Null
    $dataSubdirectoryItem = Get-Item -LiteralPath $dataSubdirectory -Force
    if (($dataSubdirectoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "The Sparky $dataSubdirectoryName directory must not be a symbolic link or junction."
    }
    & icacls.exe $dataSubdirectory /inheritance:r /grant:r `
        '*S-1-5-19:(OI)(CI)M' `
        '*S-1-5-32-544:(OI)(CI)F' `
        '*S-1-5-18:(OI)(CI)F' /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not set Sparky $dataSubdirectoryName directory permissions."
    }
}

# OpenSSH rejects private keys that are readable by unrelated users or groups.
# The directory ACL above is intentionally broader for ordinary service data,
# so give the tunnel key its own service-only ACL after applying that directory ACL.
$tunnelPrivateKeyPaths = @([IO.Path]::GetFullPath($SshPrivateKeyPath))
if ($adminTunnelEnabled) {
    $tunnelPrivateKeyPaths += [IO.Path]::GetFullPath($AdminSshPrivateKeyPath)
}
foreach ($tunnelPrivateKeyPath in $tunnelPrivateKeyPaths) {
    & icacls.exe $tunnelPrivateKeyPath /setowner '*S-1-5-19' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not set the SSH private key owner to Local Service.'
    }
    & icacls.exe $tunnelPrivateKeyPath /inheritance:r /grant:r '*S-1-5-19:R' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restrict the SSH private key to Local Service.'
    }
}

$knownHostsPaths = @([IO.Path]::GetFullPath($SshKnownHostsPath))
if ($adminTunnelEnabled) {
    $knownHostsPaths += [IO.Path]::GetFullPath($AdminSshKnownHostsPath)
}
foreach ($knownHostsPath in $knownHostsPaths) {
    & icacls.exe $knownHostsPath /setowner '*S-1-5-32-544' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not set the known-hosts pin owner to Administrators.'
    }
    & icacls.exe $knownHostsPath /inheritance:r /grant:r `
        '*S-1-5-19:R' `
        '*S-1-5-32-544:F' `
        '*S-1-5-18:F' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restrict the known-hosts pin to the service and local administrators.'
    }
}

$json = $settings | ConvertTo-Json -Depth 3
if (Test-Path -LiteralPath $fullSettingsPath) {
    $settingsFileItem = Get-Item -LiteralPath $fullSettingsPath -Force
    if (($settingsFileItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'SettingsPath must not point to a symbolic link.'
    }
}
[IO.File]::WriteAllText($fullSettingsPath, $json, [Text.UTF8Encoding]::new($false))

& icacls.exe $fullSettingsPath /inheritance:r /grant:r `
    '*S-1-5-19:R' `
    '*S-1-5-32-544:F' `
    '*S-1-5-18:F' | Out-Null
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $fullSettingsPath -Force
    throw 'Could not restrict the Sparky settings file permissions.'
}

Write-Output "Sparky settings created at $fullSettingsPath. Secret values are DPAPI protected; verify the file ACL before starting the service."
