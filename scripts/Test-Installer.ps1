param([Parameter(Mandatory = $true)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$installer = (Resolve-Path -LiteralPath $InstallerPath).ProviderPath
# Do not disturb an existing real installation of this application.
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C962902-EE40-45C8-846C-BA02FD219454}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'INSTALLER_TEST_EXISTING_INSTALLATION' }
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root ('artifacts\installer-tests\' + [Guid]::NewGuid().ToString('N'))
function Run-InstallerProcess([string]$File, [string]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.Arguments = $Arguments
    $info.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'INSTALLER_TEST_TIMEOUT' }
        if ($process.ExitCode -ne 0) { throw 'INSTALLER_TEST_EXIT_FAILED' }
    } finally { $process.Dispose() }
}
try {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR="' + $target + '"'
    Run-InstallerProcess $installer $arguments
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -AppDirectory $target
    # Reinstall the same version into the same location to exercise replacement.
    Run-InstallerProcess $installer $arguments
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -AppDirectory $target
} finally {
    $uninstaller = Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Run-InstallerProcess $uninstaller '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    }
}
if ((Test-Path -LiteralPath (Join-Path $target 'SecretHandoff.exe')) -or (Test-Path -LiteralPath $uninstallKey)) {
    throw 'INSTALLER_TEST_UNINSTALL_FAILED'
}
Write-Output 'INSTALLER_ROUNDTRIP_PASSED'
