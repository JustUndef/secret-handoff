param([Parameter(Mandatory = $true)][string]$AppDirectory)
$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppDirectory).ProviderPath
$expectedRuntime = (Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'release-runtime.json') -Raw | ConvertFrom-Json).version
$runtime = Get-Content -LiteralPath (Join-Path $app 'SecretHandoff.runtimeconfig.json') -Raw | ConvertFrom-Json
if (@($runtime.runtimeOptions.includedFrameworks).Count -ne 2) { throw 'PACKAGE_RUNTIME_MISSING' }
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    if ($framework.version -ne $expectedRuntime) { throw 'PACKAGE_RUNTIME_VERSION_FAILED' }
}
foreach ($name in @('SecretHandoff.exe', 'coreclr.dll', 'System.Windows.Forms.dll', 'Invoke-Handoff.ps1', 'LICENSE')) {
    if (-not (Test-Path -LiteralPath (Join-Path $app $name) -PathType Leaf)) { throw 'PACKAGE_FILE_MISSING' }
}
# Exercise the actual published executable without input or UI automation.
# Invalid supervision metadata suppresses error dialogs and must return category 5.
$info = [Diagnostics.ProcessStartInfo]::new()
$info.FileName = Join-Path $app 'SecretHandoff.exe'
$info.Arguments = '--request unused --watch-parent 0 0'
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$child = [Diagnostics.Process]::Start($info)
try {
    if (-not $child.WaitForExit(15000)) { $child.Kill(); throw 'PACKAGE_TIMEOUT' }
    if ($child.ExitCode -ne 5) { throw 'PACKAGE_STATUS_FAILED' }
    if ($child.StandardOutput.ReadToEnd().Length -ne 0 -or $child.StandardError.ReadToEnd().Length -ne 0) { throw 'PACKAGE_OUTPUT_FAILED' }
} finally { $child.Dispose() }
Write-Output 'PACKAGE_SMOKE_PASSED'
