<#
.SYNOPSIS
    End-to-end smoke test: builds the solution, starts the server, asserts on its
    startup log, shuts it down gracefully and asserts on shutdown.

.DESCRIPTION
    Exit codes:
      0  pass
      2  build failed
      3  GameRoot not found
      4  timed out waiting for the server to come online
      5  a forbidden pattern was found in the log
      6  the server did not shut down gracefully
      7  unexpected error
#>
[CmdletBinding()]
param(
    [string] $GameRoot = $env:PERPETUUM_GAMEROOT,
    [string] $Configuration = 'Release',
    [int]    $Timeout = 180,
    [int]    $SettleQuietSeconds = 10,
    [int]    $SettleTimeout = 120,
    [int]    $ShutdownTimeout = 120,
    [switch] $KeepLog
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Any terminating error that no phase handles leaves through a documented code rather than
# PowerShell's own. Without this, a missing dotnet, an unreadable log, or a failed P/Invoke
# would exit with a code the script never documented.
trap {
    Write-Host "Unexpected error: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 7
}

# Patterns that must appear. Absence fails the run.
$RequiredOnline   = 'State : \[Online\]'
$RequiredOffline  = 'State : \[Off\]'

# Patterns that must not appear anywhere in the startup log. Presence fails the run.
$ForbiddenPatterns = @(
    'Unhandled exception',
    'System\.InvalidOperationException',
    'System\.NullReferenceException',
    'The current TransactionScope is already complete',
    'nesting level exceeded'
)

# Values that are measured and printed, never asserted. These legitimately change with
# every content patch, so asserting on them would fail when the game works. Each entry is
# counted by matching lines, because the server logs one line per event and never emits a
# total. The strings are taken from a real startup log, not invented.
$ReportedCounters = [ordered]@{
    'members spawned' = 'member spawned to zone'
    'flock batches'   = 'NPCs created'
}

$repoRoot = Split-Path -Parent $PSScriptRoot

function Write-Section([string] $Text) {
    Write-Host ''
    Write-Host "== $Text" -ForegroundColor Cyan
}

# --- Phase 0: environment -------------------------------------------------
Write-Section 'Environment'
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    Write-Host 'GameRoot not supplied and PERPETUUM_GAMEROOT is not set.' -ForegroundColor Red
    exit 3
}
if (-not (Test-Path -LiteralPath $GameRoot)) {
    Write-Host "GameRoot not found: $GameRoot" -ForegroundColor Red
    exit 3
}
Write-Host "GameRoot      : $GameRoot"
Write-Host "Configuration : $Configuration"

# --- Phase 1: build -------------------------------------------------------
Write-Section 'Build'
& dotnet build (Join-Path $repoRoot 'PerpetuumServer2.sln') -c $Configuration -p:Platform=x64 --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code $LASTEXITCODE." -ForegroundColor Red
    exit 2
}

# --- Phase 2: launch ------------------------------------------------------
Write-Section 'Launch'
# Perpetuum.Server has no BaseOutputPath override, so it builds to its own project folder.
# Only Perpetuum.ServerService2.csproj redirects output to the repo-root bin\ directory, which
# is why CLAUDE.md's blanket "Output: bin/x64/Release/net8.0" does not hold for this project.
$serverExe = Join-Path $repoRoot "src\Perpetuum.Server\bin\x64\$Configuration\net8.0\Perpetuum.Server.exe"
if (-not (Test-Path -LiteralPath $serverExe)) {
    Write-Host "Server executable not found: $serverExe" -ForegroundColor Red
    exit 2
}

$logPath = Join-Path ([System.IO.Path]::GetTempPath()) "perpetuum-smoke-$(Get-Date -Format yyyyMMdd-HHmmss).log"
$errPath = "$logPath.err"
Write-Host "Log           : $logPath"

$proc = Start-Process -FilePath $serverExe -ArgumentList "`"$GameRoot`"" `
    -RedirectStandardOutput $logPath -RedirectStandardError $errPath `
    -PassThru -WindowStyle Hidden

# Start-Process -PassThru combined with output redirection returns a Process object whose
# ExitCode (and Handle) cannot be read later, in this PowerShell version. Open our own handle
# now, while the process is guaranteed to still be running, so Phase 5 can read the real exit
# code via GetExitCodeProcess. Both Win32 return values are checked below: an unchecked failure
# here would silently leave the exit-code variable at its default and report a graceful
# shutdown that never happened. Either failure is a failed P/Invoke, which is exactly what the
# trap above exists to turn into a documented exit 7 instead of a default PowerShell exit.
$procApiSignature = @'
[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
'@
$procApi = Add-Type -MemberDefinition $procApiSignature -Name 'SmokeProcess' -Namespace 'Perpetuum' -PassThru
$PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
$procHandle = $procApi::OpenProcess($PROCESS_QUERY_LIMITED_INFORMATION, $false, [uint32] $proc.Id)
if ($procHandle -eq [IntPtr]::Zero) {
    $lastError = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "OpenProcess failed for PID $($proc.Id) (Win32 error $lastError). Cannot verify the server's exit code later."
}

# --- Phase 3: wait for online --------------------------------------------
Write-Section 'Waiting for [Online]'
$deadline = (Get-Date).AddSeconds($Timeout)
$online = $false
$startedAt = Get-Date
while ((Get-Date) -lt $deadline) {
    if ($proc.HasExited) { break }
    if (Test-Path -LiteralPath $logPath) {
        $content = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
        if ($content -and $content -match $RequiredOnline) { $online = $true; break }
    }
    Start-Sleep -Milliseconds 500
}
$elapsed = [int]((Get-Date) - $startedAt).TotalSeconds

if (-not $online) {
    Write-Host "Server did not reach [Online] within $Timeout seconds." -ForegroundColor Red
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Write-Host "Log kept for inspection: $logPath" -ForegroundColor Yellow
    exit 4
}
Write-Host "Online after $elapsed seconds." -ForegroundColor Green

# --- Phase 3b: wait for startup to drain ---------------------------------
# The server reports [Online] well before it finishes spawning NPCs — measured at 34 seconds
# of continued spawning past [Online] on a real run. Sending Ctrl+C at [Online] therefore
# starts a shutdown that has to contend with the rest of startup, and it overruns any sane
# shutdown budget. Wait until the log stops growing before shutting down.
Write-Section 'Waiting for startup to drain'
$settleDeadline = (Get-Date).AddSeconds($SettleTimeout)
$settleStartedAt = Get-Date
$lastSize = -1
$quietSince = Get-Date
while ((Get-Date) -lt $settleDeadline) {
    if ($proc.HasExited) { break }
    $size = (Get-Item -LiteralPath $logPath).Length
    if ($size -ne $lastSize) {
        $lastSize = $size
        $quietSince = Get-Date
    } elseif (((Get-Date) - $quietSince).TotalSeconds -ge $SettleQuietSeconds) {
        break
    }
    Start-Sleep -Milliseconds 500
}
$settleElapsed = [int]((Get-Date) - $settleStartedAt).TotalSeconds
Write-Host "Log quiet for $SettleQuietSeconds s after $settleElapsed s. Proceeding to shutdown."

# --- Phase 4: assert on the startup log ----------------------------------
Write-Section 'Startup assertions'
$startupLog = Get-Content -LiteralPath $logPath -Raw
$violations = @()
foreach ($pattern in $ForbiddenPatterns) {
    if ($startupLog -match $pattern) { $violations += $pattern }
}

Write-Host 'Reported values (not asserted):'
foreach ($key in $ReportedCounters.Keys) {
    $count = ([regex]::Matches($startupLog, $ReportedCounters[$key])).Count
    Write-Host ("  {0,-18}: {1}" -f $key, $count)
}
Write-Host ("  {0,-18}: {1}s" -f 'time to online', $elapsed)
Write-Host ("  {0,-18}: {1}s" -f 'time to settle', $settleElapsed)

# --- Phase 5: graceful shutdown ------------------------------------------
Write-Section 'Shutdown'
$signature = @'
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(uint dwProcessId);
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
[DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
[DllImport("kernel32.dll")] public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
'@
$kernel = Add-Type -MemberDefinition $signature -Name 'SmokeConsole' -Namespace 'Perpetuum' -PassThru

$ATTACH_PARENT_PROCESS = [uint32]::MaxValue
$graceful = $false
$attached = $false
$shutdownStartedAt = Get-Date
try {
    # A process that already owns a console cannot attach to another one: AttachConsole
    # returns false with ERROR_ACCESS_DENIED (5). Release our own console first.
    [void] $kernel::FreeConsole()
    $attached = $kernel::AttachConsole([uint32] $proc.Id)
    if ($attached) {
        # MUST come before the event, or the Ctrl+C kills this PowerShell session.
        [void] $kernel::SetConsoleCtrlHandler([IntPtr]::Zero, $true)
        [void] $kernel::GenerateConsoleCtrlEvent(0, 0)
        $graceful = $proc.WaitForExit($ShutdownTimeout * 1000)
    }
} finally {
    # No Write-Host may run between our FreeConsole above and this restore: with no console
    # attached, writing to the host throws.
    [void] $kernel::FreeConsole()
    [void] $kernel::AttachConsole($ATTACH_PARENT_PROCESS)
    [void] $kernel::SetConsoleCtrlHandler([IntPtr]::Zero, $false)
}
$shutdownElapsed = [int]((Get-Date) - $shutdownStartedAt).TotalSeconds
if (-not $attached) {
    Write-Host 'AttachConsole failed; cannot deliver Ctrl+C.' -ForegroundColor Yellow
}

$finalLog = Get-Content -LiteralPath $logPath -Raw
Write-Host ("Shutdown took {0}s." -f $shutdownElapsed)
if (-not $graceful) {
    Write-Host "Server did not exit within $ShutdownTimeout seconds of Ctrl+C. Killing it." -ForegroundColor Red
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Write-Host "Log kept for inspection: $logPath" -ForegroundColor Yellow
    exit 6
}
if ($finalLog -notmatch $RequiredOffline) {
    Write-Host 'Server exited but never reported [Off].' -ForegroundColor Red
    Write-Host "Log kept for inspection: $logPath" -ForegroundColor Yellow
    exit 6
}
[uint32] $serverExitCode = 0
$gotExitCode = $procApi::GetExitCodeProcess($procHandle, [ref] $serverExitCode)
if (-not $gotExitCode) {
    $lastError = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "GetExitCodeProcess failed for PID $($proc.Id) (Win32 error $lastError). Cannot verify the server's exit code."
}
if ($serverExitCode -ne 0) {
    Write-Host "Server exit code was $serverExitCode, expected 0." -ForegroundColor Red
    Write-Host "Log kept for inspection: $logPath" -ForegroundColor Yellow
    exit 6
}
Write-Host 'Graceful shutdown confirmed.' -ForegroundColor Green

# --- Phase 6: verdict -----------------------------------------------------
Write-Section 'Verdict'
if ($violations.Count -gt 0) {
    Write-Host 'Forbidden patterns found in the log:' -ForegroundColor Red
    foreach ($v in $violations) { Write-Host "  $v" -ForegroundColor Red }
    Write-Host "Log kept for inspection: $logPath" -ForegroundColor Yellow
    exit 5
}

Write-Host 'SMOKE TEST PASSED' -ForegroundColor Green
if (-not $KeepLog) {
    Remove-Item -LiteralPath $logPath -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $errPath -ErrorAction SilentlyContinue
} else {
    Write-Host "Log kept: $logPath"
}
exit 0
