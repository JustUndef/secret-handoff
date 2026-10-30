param([Parameter(Mandatory = $true)][string]$RequestPath)
$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'SecretHandoff.exe'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    $launcher = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\demo\SecretHandoff.exe'
}
# Optional JSON compatibility. Direct invocation preserves target stdout/stderr.
# No caller binding, status banner, secret handling, or process supervision lives here.
try {
    $request = (Resolve-Path -LiteralPath $RequestPath).ProviderPath
    & $launcher --request $request
    exit $LASTEXITCODE
} catch {
    [Console]::Error.WriteLine('INTERNAL_ERROR: Invocation failed.')
    exit 8
}
