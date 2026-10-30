param([switch]$SkipTests, [string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = $DotnetPath
if (-not $dotnet) {
    $userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $userDotnet -PathType Leaf) { $dotnet = $userDotnet }
    else { $dotnet = (Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
}
if (-not $dotnet -or -not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'BUILD_SDK_MISSING' }
$buildEnvironment = @{
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    DOTNET_CLI_HOME = Join-Path $projectRoot '.build\dotnet-home'
    NUGET_PACKAGES = Join-Path $projectRoot '.build\packages'
    MSBuildEnableWorkloadResolver = 'false'
    SHANDOFF_TEST_POWERSHELL = (Get-Process -Id $PID).Path
}
$previous = @{}
foreach ($key in $buildEnvironment.Keys) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $buildEnvironment[$key], 'Process')
}
Push-Location -LiteralPath $projectRoot
try {
    & $dotnet build SecretHandoff.slnx --nologo -m:1 -nr:false -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'PROTOTYPE_BUILD_FAILED' }
    $demo = Join-Path $projectRoot 'artifacts\demo'
    $probeDirectory = Join-Path $demo 'probe'
    [void](New-Item -ItemType Directory -Path $demo, $probeDirectory -Force)
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\SecretHandoff\bin\Debug\net10.0-windows') -File |
        Where-Object Extension -In '.exe', '.dll', '.json' | Copy-Item -Destination $demo
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests\SyntheticProbe\bin\Debug\net10.0-windows') -File |
        Where-Object Extension -In '.exe', '.dll', '.json' | Copy-Item -Destination $probeDirectory
    $request = [ordered]@{
        executable = Join-Path $probeDirectory 'SyntheticProbe.exe'
        arguments = @('exchange')
        workingDirectory = $probeDirectory
        secrets = @(
            @{ name = 'EXAMPLE_API_TOKEN'; description = 'First synthetic test value from the instructions' }
            @{ name = 'SECOND_SECRET'; description = 'Second synthetic test value from the instructions' }
        )
    }
    [IO.File]::WriteAllText((Join-Path $demo 'synthetic.handoff.json'), ($request | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    $launch = '@echo off' + "`r`n" + '"%~dp0SecretHandoff.exe" --request "%~dp0synthetic.handoff.json"' + "`r`n" + 'echo Launcher exit: %ERRORLEVEL%' + "`r`n" + 'pause' + "`r`n"
    [IO.File]::WriteAllText((Join-Path $demo 'Start-Synthetic-Demo.cmd'), $launch, [Text.Encoding]::ASCII)
    if (-not $SkipTests) {
        & (Join-Path $projectRoot 'tests\SecretHandoff.Tests\bin\Debug\net10.0-windows\SecretHandoff.Tests.exe')
        if ($LASTEXITCODE -ne 0) { throw 'SYNTHETIC_TESTS_FAILED' }
    }
    Write-Output 'PROTOTYPE_READY: artifacts\demo\Start-Synthetic-Demo.cmd'
} finally {
    Pop-Location
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
