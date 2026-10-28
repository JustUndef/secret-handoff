param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [string]$IsccPath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = (Resolve-Path -LiteralPath $PublishDirectory).ProviderPath
if (-not $IsccPath) { $IsccPath = (Get-Command ISCC.exe -ErrorAction Stop).Source }
$id = [Guid]::NewGuid().ToString('N')
$stage = Join-Path $root "artifacts\installer-path-tests\$id"
$environmentKey = "Software\SecretHandoff\PathTests\$id\Environment"
$fixtureKey = "Software\SecretHandoff\PathTests\$id"
$pathOwnerKey = "$fixtureKey\Installer"
$ownerKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C962902-EE40-45C8-846C-BA02FD219454}_is1'
if ([Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($ownerKey)) { throw 'PATH_TEST_EXISTING_INSTALLATION' }
[void](New-Item -ItemType Directory -Path $stage -Force)
$target = Join-Path $stage 'Installed App'
$installer = Join-Path $stage 'SecretHandoff-0.0.0-rc.0-win-x64-Setup.exe'

function Assert-PathTest([bool]$Condition, [string]$Category) {
    if (-not $Condition) { throw $Category }
}
function Get-TestPath {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($environmentKey)
    if (-not $key) { return @{ Exists = $false; Value = ''; Kind = $null } }
    try {
        $exists = $key.GetValueNames() -contains 'Path'
        if (-not $exists) { return @{ Exists = $false; Value = ''; Kind = $null } }
        return @{
            Exists = $true
            Value = $key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            Kind = $key.GetValueKind('Path')
        }
    } finally { $key.Dispose() }
}
function Set-TestPath([string]$Value, [Microsoft.Win32.RegistryValueKind]$Kind = 'ExpandString') {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($environmentKey)
    try { $key.SetValue('Path', $Value, $Kind) } finally { $key.Dispose() }
}
function Invoke-TestSetup([string]$Tasks = '') {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $installer
    $info.Arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR="' + $target + '" ' + $Tasks
    $info.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'PATH_TEST_TIMEOUT' }
        Assert-PathTest ($process.ExitCode -eq 0) 'PATH_TEST_SETUP_FAILED'
    } finally { $process.Dispose() }
}
function Invoke-TestUninstall {
    $file = Join-Path $target 'unins000.exe'
    if (-not (Test-Path -LiteralPath $file)) { return }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $file
    $info.Arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    $info.UseShellExecute = $false
    # The original uninstaller delegates to a temporary executable. Wait for its process tree.
    $process = Start-Process -FilePath $info.FileName -ArgumentList $info.Arguments -PassThru -Wait -WindowStyle Hidden
    try { Assert-PathTest ($process.ExitCode -eq 0) 'PATH_TEST_UNINSTALL_FAILED' }
    finally { $process.Dispose() }
}

try {
    # Same installer code, with only the environment registry subkey redirected at compile time.
    & $IsccPath /Q '/DAppVersion=0.0.0-rc.0' "/DPublishDir=$publish" "/DReleaseDir=$stage" `
        "/DPathEnvironmentKey=$environmentKey" "/DPathOwnerKey=$pathOwnerKey" (Join-Path $root 'installer\SecretHandoff.iss')
    Assert-PathTest ($LASTEXITCODE -eq 0) 'PATH_TEST_COMPILE_FAILED'

    $original = 'C:\Synthetic\One;%SYNTHETIC_ROOT%\bin;;'
    Set-TestPath $original
    Invoke-TestSetup
    Assert-PathTest ((Get-TestPath).Value -ceq $original) 'PATH_TEST_DEFAULT_CHANGED'
    Invoke-TestUninstall
    Assert-PathTest ((Get-TestPath).Value -ceq $original) 'PATH_TEST_DEFAULT_UNINSTALL_CHANGED'
    Write-Output 'PASS installer-path-default-off'

    Invoke-TestSetup '/TASKS="userpath"'
    Assert-PathTest ((Get-TestPath).Value -ceq ($original + ';' + $target)) 'PATH_TEST_APPEND_FAILED'
    Assert-PathTest ((Get-TestPath).Kind -eq 'ExpandString') 'PATH_TEST_KIND_CHANGED'
    Invoke-TestSetup # Previous checkbox selection is retained, without duplicating the entry.
    Assert-PathTest ((Get-TestPath).Value -ceq ($original + ';' + $target)) 'PATH_TEST_DUPLICATE'
    Set-TestPath ($original + ';' + $target + ';C:\Synthetic\Later')
    Invoke-TestUninstall
    Assert-PathTest ((Get-TestPath).Value -ceq ($original + ';C:\Synthetic\Later')) 'PATH_TEST_FOREIGN_ENTRY_CHANGED'
    Write-Output 'PASS installer-path-add-reinstall-preserve-later-entry'

    $preexisting = $original + ';"' + $target.ToLowerInvariant() + '\"'
    Set-TestPath $preexisting
    Invoke-TestSetup '/TASKS="userpath"'
    Assert-PathTest ((Get-TestPath).Value -ceq $preexisting) 'PATH_TEST_PREEXISTING_CHANGED'
    Invoke-TestUninstall
    Assert-PathTest ((Get-TestPath).Value -ceq $preexisting) 'PATH_TEST_PREEXISTING_REMOVED'
    Write-Output 'PASS installer-path-preexisting-unowned'

    Set-TestPath $original 'String'
    Invoke-TestSetup '/TASKS="userpath"'
    Assert-PathTest ((Get-TestPath).Kind -eq 'String') 'PATH_TEST_STRING_KIND_CHANGED'
    Invoke-TestSetup '/TASKS=""'
    Assert-PathTest ((Get-TestPath).Value -ceq $original) 'PATH_TEST_DISABLE_FAILED'
    Invoke-TestUninstall
    Assert-PathTest ((Get-TestPath).Kind -eq 'String') 'PATH_TEST_UNINSTALL_KIND_CHANGED'
    Write-Output 'PASS installer-path-disable-preserves-string-kind'

    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($environmentKey, $true)
    try { $key.DeleteValue('Path') } finally { $key.Dispose() }
    Invoke-TestSetup '/TASKS="userpath"'
    Assert-PathTest ((Get-TestPath).Value -ceq $target) 'PATH_TEST_CREATE_FAILED'
    Invoke-TestUninstall
    Assert-PathTest (-not (Get-TestPath).Exists) 'PATH_TEST_MISSING_NOT_RESTORED'
    Write-Output 'PASS installer-path-originally-missing'

    Set-TestPath ''
    Invoke-TestSetup '/TASKS="userpath"'
    Invoke-TestUninstall
    Assert-PathTest ((Get-TestPath).Exists -and (Get-TestPath).Value -ceq '') 'PATH_TEST_EMPTY_NOT_RESTORED'
    Write-Output 'PASS installer-path-originally-empty'
    Write-Output 'INSTALLER_PATH_TESTS_PASSED'
} finally {
    Invoke-TestUninstall
    # Only the fixed synthetic subtree with this generated ID is removed. No user environment data.
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($fixtureKey, $false)
}
