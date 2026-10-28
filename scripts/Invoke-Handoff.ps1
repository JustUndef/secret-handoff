param([Parameter(Mandatory = $true)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $PSScriptRoot 'SecretHandoff.exe'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    $launcher = Join-Path $projectRoot 'artifacts\demo\SecretHandoff.exe'
}
$child = $null
$code = 8
$invocationFailed = $false
try {
    # Metadata path only. PowerShell 7 and 5.1 compatible: quotes cannot be part of a Windows path.
    $request = (Resolve-Path -LiteralPath $RequestPath).ProviderPath
    if ($request.Contains('"')) { throw 'INVALID_REQUEST' }
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $launcher
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $owner = [System.Diagnostics.Process]::GetCurrentProcess()
    try { $ownerStart = $owner.StartTime.ToUniversalTime().ToFileTimeUtc() }
    finally { $owner.Dispose() }
    # Lifetime metadata only; the launcher opens and verifies this exact process before accepting input.
    $info.Arguments = '--request "' + $request + '" --watch-parent ' +
        $PID.ToString([Globalization.CultureInfo]::InvariantCulture) + ' ' +
        $ownerStart.ToString([Globalization.CultureInfo]::InvariantCulture)
    $child = [System.Diagnostics.Process]::Start($info)
    while (-not $child.WaitForExit(200)) { }
    $code = $child.ExitCode
} catch {
    $invocationFailed = $true
    $code = 8
} finally {
    if ($child) {
        try {
            if (-not $child.HasExited) {
                $child.Kill()
                if (-not $child.WaitForExit(5000)) { $code = 9 }
            }
        } catch { $code = 9 }
        finally { $child.Dispose() }
    }
}
# The launcher discards target stdout/stderr; CLI configuration failures use fixed diagnostics.
# This wrapper's own stdout is restricted to fixed status categories.
if ($invocationFailed -and $code -eq 8) { Write-Output 'HANDOFF_INVOKE_FAILED' }
else { Write-Output ('HANDOFF_EXIT_{0}' -f $code) }
exit $code
