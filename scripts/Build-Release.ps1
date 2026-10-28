param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$DotnetPath,
    [string]$IsccPath
)
$ErrorActionPreference = 'Stop'
# Restricted SemVer subset, also safe as an Inno preprocessor value and filename.
if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-(alpha|beta|rc)\.(0|[1-9][0-9]*))?$') {
    throw 'RELEASE_VERSION_INVALID'
}
$numericVersion = $Version.Split('-')[0]
foreach ($part in $numericVersion.Split('.')) {
    if ([long]$part -gt 65534) { throw 'RELEASE_VERSION_INVALID' }
}
$root = Split-Path -Parent $PSScriptRoot
if (-not $DotnetPath) { $DotnetPath = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe' }
if (-not $IsccPath) { $IsccPath = (Get-Command ISCC.exe -ErrorAction Stop).Source }
if (-not (Test-Path -LiteralPath $DotnetPath -PathType Leaf)) { throw 'RELEASE_SDK_MISSING' }
# Every build gets a fresh directory; never package stale outputs.
$stage = Join-Path $root ('artifacts\release-builds\' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $stage 'app'
$release = Join-Path $stage 'release'
[void](New-Item -ItemType Directory -Path $publish, $release -Force)
$previous = @{}
$settings = @{
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    DOTNET_CLI_HOME = Join-Path $root '.build\dotnet-home'
    NUGET_PACKAGES = Join-Path $root '.build\packages'
    MSBuildEnableWorkloadResolver = 'false'
}
foreach ($key in $settings.Keys) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
}
Push-Location -LiteralPath $root
try {
    # Only release restore uses the official public feed for the bundled runtime packs.
    & $DotnetPath restore src/SecretHandoff/SecretHandoff.csproj -r win-x64 --source https://api.nuget.org/v3/index.json -p:SelfContained=true -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'RELEASE_RESTORE_FAILED' }
    & $DotnetPath publish src/SecretHandoff/SecretHandoff.csproj -c Release -r win-x64 --self-contained true --no-restore -o $publish `
        "-p:Version=$Version" "-p:AssemblyVersion=$numericVersion.0" "-p:FileVersion=$numericVersion.0" `
        -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false -m:1 -nr:false -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'RELEASE_PUBLISH_FAILED' }
    foreach ($file in @('README.md', 'ARCHITECTURE.md', 'TESTING.md', 'RELEASING.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $publish
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Invoke-Handoff.ps1') -Destination $publish
    & $IsccPath /Q "/DAppVersion=$Version" "/DPublishDir=$publish" "/DReleaseDir=$release" installer/SecretHandoff.iss
    if ($LASTEXITCODE -ne 0) { throw 'RELEASE_INSTALLER_FAILED' }
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $release "SecretHandoff-$Version-win-x64.zip")
    $lines = Get-ChildItem -LiteralPath $release -File | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
    }
    [IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), [string[]]$lines, [Text.UTF8Encoding]::new($false))
    Write-Output "RELEASE_READY: $release"
    if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "release_dir=$release" }
} finally {
    Pop-Location
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
