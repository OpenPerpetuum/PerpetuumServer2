[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ServiceIdentity,

    [string]$SettingsPath = (Join-Path $env:ProgramData 'Sparky\vdm-client-settings.json')
)

$ErrorActionPreference = 'Stop'

$principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated Windows PowerShell session.'
}

# Windows PowerShell 5.1 exposes ProtectedData through System.Security.dll.
# Loading the newer System.Security.Cryptography.ProtectedData assembly by name fails there.
Add-Type -AssemblyName System.Security

$serviceSid = (New-Object System.Security.Principal.NTAccount($ServiceIdentity)).Translate(
    [System.Security.Principal.SecurityIdentifier])
$systemSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
$administratorsSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')

$fullPath = [System.IO.Path]::GetFullPath($SettingsPath)
$protectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:ProgramData 'Sparky'))
$settingsDirectory = [System.IO.Path]::GetDirectoryName($fullPath)
if (-not $settingsDirectory.Equals($protectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "SettingsPath must be a direct file inside $protectedRoot."
}

function Assert-NoReparsePointComponents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($normalizedPath)
    $currentPath = $pathRoot
    $rootItem = Get-Item -LiteralPath $currentPath -Force -ErrorAction Stop
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to use a reparse-point path component: $currentPath"
    }

    $separatorCharacters = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $segments = $normalizedPath.Substring($pathRoot.Length).Split(
        $separatorCharacters,
        [System.StringSplitOptions]::RemoveEmptyEntries)

    foreach ($segment in $segments) {
        $currentPath = Join-Path $currentPath $segment
        try {
            $pathItem = Get-Item -LiteralPath $currentPath -Force -ErrorAction Stop
        }
        catch [System.Management.Automation.ItemNotFoundException] {
            continue
        }

        if (($pathItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to use a reparse-point path component: $currentPath"
        }
    }
}

Assert-NoReparsePointComponents -Path $fullPath

$directory = $settingsDirectory
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
Assert-NoReparsePointComponents -Path $fullPath

$directoryAcl = New-Object System.Security.AccessControl.DirectorySecurity
$directoryAcl.SetAccessRuleProtection($true, $false)
$directoryInheritance = [System.Security.AccessControl.InheritanceFlags](
    [int][System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
    [int][System.Security.AccessControl.InheritanceFlags]::ObjectInherit)
$serviceDirectoryRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
    $serviceSid,
    [System.Security.AccessControl.FileSystemRights]::ReadAndExecute,
    $directoryInheritance,
    [System.Security.AccessControl.PropagationFlags]::None,
    [System.Security.AccessControl.AccessControlType]::Allow)
$directoryAcl.AddAccessRule($serviceDirectoryRule)
foreach ($sid in @($systemSid, $administratorsSid)) {
    $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $sid,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        $directoryInheritance,
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $directoryAcl.AddAccessRule($rule)
}
$directoryAcl.SetOwner($administratorsSid)
Set-Acl -LiteralPath $directory -AclObject $directoryAcl

$secureToken = Read-Host 'Enter the Sparky API bearer token' -AsSecureString
if ($secureToken.Length -lt 16) {
    throw 'The token is empty or too short.'
}

$bstr = [IntPtr]::Zero
$tokenCharacters = $null
$tokenBytes = $null
$protectedBytes = $null
try {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    $characterCount = [Runtime.InteropServices.Marshal]::ReadInt32($bstr, -4) / 2
    $tokenCharacters = New-Object char[] $characterCount
    [Runtime.InteropServices.Marshal]::Copy($bstr, $tokenCharacters, 0, $characterCount)
    $tokenBytes = [System.Text.Encoding]::UTF8.GetBytes($tokenCharacters)
    $protectedBytes = [Security.Cryptography.ProtectedData]::Protect(
        $tokenBytes,
        $null,
        [Security.Cryptography.DataProtectionScope]::LocalMachine)

    $settings = @{
        version = 1
        protectedToken = [Convert]::ToBase64String($protectedBytes)
    } | ConvertTo-Json -Compress

    if (-not (Test-Path -LiteralPath $fullPath)) {
        $fileStream = [System.IO.File]::Open(
            $fullPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        $fileStream.Dispose()
    }

    Assert-NoReparsePointComponents -Path $fullPath
    $fileAcl = New-Object System.Security.AccessControl.FileSecurity
    $fileAcl.SetAccessRuleProtection($true, $false)
    $serviceReadRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $serviceSid,
        [System.Security.AccessControl.FileSystemRights]::Read,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $fileAcl.AddAccessRule($serviceReadRule)
    foreach ($sid in @($systemSid, $administratorsSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $fileAcl.AddAccessRule($rule)
    }
    $fileAcl.SetOwner($administratorsSid)
    Set-Acl -LiteralPath $fullPath -AclObject $fileAcl
    Assert-NoReparsePointComponents -Path $fullPath
    [System.IO.File]::WriteAllText($fullPath, $settings, [System.Text.Encoding]::UTF8)

    Write-Output "Sparky protected client settings written to $fullPath for service identity $ServiceIdentity."
}
finally {
    if ($tokenBytes) {
        [Array]::Clear($tokenBytes, 0, $tokenBytes.Length)
    }
    if ($tokenCharacters) {
        [Array]::Clear($tokenCharacters, 0, $tokenCharacters.Length)
    }
    if ($protectedBytes) {
        [Array]::Clear($protectedBytes, 0, $protectedBytes.Length)
    }
    if ($bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    if ($secureToken) {
        $secureToken.Dispose()
    }
}
