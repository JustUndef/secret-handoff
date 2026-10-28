param([Parameter(Mandatory = $true)][string]$AppDirectory)
$ErrorActionPreference = 'Stop'
$app = (Resolve-Path -LiteralPath $AppDirectory).ProviderPath
$expectedRuntime = (Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'release-runtime.json') -Raw | ConvertFrom-Json).version
$runtime = Get-Content -LiteralPath (Join-Path $app 'SecretHandoff.runtimeconfig.json') -Raw | ConvertFrom-Json
if (@($runtime.runtimeOptions.includedFrameworks).Count -ne 2) { throw 'PACKAGE_RUNTIME_MISSING' }
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    if ($framework.version -ne $expectedRuntime) { throw 'PACKAGE_RUNTIME_VERSION_FAILED' }
}
foreach ($name in @('SecretHandoff.exe', 'coreclr.dll', 'System.Windows.Forms.dll', 'Invoke-Handoff.ps1', 'LICENSE', 'README.md', 'AGENTS.md', 'CONTRIBUTING.md', 'SECURITY.md', 'THIRD-PARTY-NOTICES.md',
    'third-party\microsoft.netcore.app.runtime.win-x64\LICENSE.TXT', 'third-party\microsoft.netcore.app.runtime.win-x64\THIRD-PARTY-NOTICES.TXT',
    'third-party\microsoft.windowsdesktop.app.runtime.win-x64\LICENSE', 'third-party\microsoft.netcore.app.host.win-x64\LICENSE.TXT',
    'third-party\microsoft.netcore.app.host.win-x64\THIRD-PARTY-NOTICES.TXT')) {
    if (-not (Test-Path -LiteralPath (Join-Path $app $name) -PathType Leaf)) { throw 'PACKAGE_FILE_MISSING' }
}
# Exercise the actual published CLI without a request dialog or secret input.
# Help is intentional stdout; invalid configuration uses a bounded, fixed-category stderr line.
foreach ($mode in @('help', 'invalid')) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = Join-Path $app 'SecretHandoff.exe'
    $info.Arguments = if ($mode -eq 'help') { '--help' } else { '--request SHANDOFF_PACKAGE_METADATA --watch-parent 0 0' }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $child = [Diagnostics.Process]::Start($info)
    try {
        $stdoutTask = $child.StandardOutput.ReadToEndAsync()
        $stderrTask = $child.StandardError.ReadToEndAsync()
        if (-not $child.WaitForExit(15000)) {
            $child.Kill($true)
            [void]$child.WaitForExit(5000)
            throw 'PACKAGE_TIMEOUT'
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($mode -eq 'help') {
            if ($child.ExitCode -ne 0) { throw 'PACKAGE_HELP_STATUS_FAILED' }
            if ($stderr.Length -ne 0 -or -not $stdout.Contains('--secret') -or -not $stdout.Contains('--request') -or $stdout.Length -gt 16384) { throw 'PACKAGE_HELP_OUTPUT_FAILED' }
        } else {
            if ($child.ExitCode -ne 5) { throw 'PACKAGE_STATUS_FAILED' }
            if ($stdout.Length -ne 0 -or $stderr -cnotmatch '\AINVALID_REQUEST: [^\r\n]{1,512}\r?\n\z' -or $stderr.Contains('SHANDOFF_PACKAGE_METADATA')) { throw 'PACKAGE_OUTPUT_FAILED' }
        }
    } finally { $child.Dispose() }
}
Write-Output 'PACKAGE_SMOKE_PASSED'
