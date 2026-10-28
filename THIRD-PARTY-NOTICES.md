# Third-party components

Secret Handoff's own source and documentation use the repository's [MIT License](LICENSE). Component-specific license and attribution files are retained separately; this document does not replace them.

## Bundled .NET components

Self-contained Windows packages include .NET runtime, Windows Desktop runtime, and apphost components. Their version is selected by `release-runtime.json`. During packaging, `scripts/Build-Release.ps1` copies the original license/notice files verbatim from the restored NuGet packages into:

- `third-party/microsoft.netcore.app.runtime.win-x64/`
- `third-party/microsoft.windowsdesktop.app.runtime.win-x64/`
- `third-party/microsoft.netcore.app.host.win-x64/`

The .NET runtime and host packages supply `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`; the Windows Desktop package supplies `LICENSE`. These files travel with both the portable ZIP and installer output. Generated copies are not committed; the build extracts them from the packages for the pinned version. Packaging fails if required files are absent.

Upstream references: [.NET license information](https://github.com/dotnet/core/blob/main/license-information.md) and [.NET distribution notice guidance](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md). Review the actual component files when updating the runtime; do not assume the application's license replaces upstream terms.

## Development tools

The .NET SDK, PowerShell, GitHub Actions, and Inno Setup are development or packaging tools, not new source dependencies. Their own terms apply. The project has no direct third-party NuGet package references. Review licenses and attribution requirements before adding dependencies or other externally sourced assets.
