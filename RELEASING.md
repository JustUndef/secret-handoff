# Installation and releases

## Windows 11 x64

[GitHub releases](https://github.com/JustUndef/secret-handoff/releases) contain the installer, a portable ZIP, and `SHA256SUMS.txt` after a successful tag build. No GitHub release had been published at the time of the October 28, 2026 repository review; successful manual workflow runs provide build artifacts instead. Packages are currently unsigned. Installers and packaging do not establish full MVP acceptance; outstanding checks are listed in [TESTING.md](TESTING.md).

The installer uses English messages, installs for the current user under `%LOCALAPPDATA%\Programs\Secret Handoff`, adds Start menu entries for the application and documentation, and supports normal Windows uninstallation. It requires no administrator privileges, registers no file association, and starts no request automatically. Alternatively, extract the ZIP into a directory of your choice. Both formats include .NET; no separate runtime installation is required.

The optional **Add Secret Handoff to the user PATH (new terminals)** checkbox is unchecked on a new installation. If selected, the installer adds only its installation directory to the user PATH (`HKCU\Environment\Path`); the system PATH is unchanged. Reinstallation preserves the previous selection. Pre-existing entries are not adopted as installer-owned or removed during uninstallation. Deselecting the option during reinstallation, or uninstalling, removes only the unchanged entry previously added by this installer. Other entries and the registry string type are preserved; no old full PATH snapshot is restored.

Afterward, `SecretHandoff.exe` can be invoked without its installation path. Windows is notified of the environment change. Existing terminals and Codex retain their previous environment: fully close and reopen the terminal or terminal application, and Codex if necessary. This option does not change the restricted environment of approved target processes. Secret values are never added to PATH. An installation directory containing a semicolon cannot be used with this option.

For silent installation, `/TASKS="userpath"` enables the option; `/TASKS=""` explicitly disables it. The portable ZIP does not modify PATH. Test details: [INSTALLER-TESTING.md](INSTALLER-TESTING.md).

The Start menu entry opens the optional request file picker. Agents invoke the EXE directly, using the separately installed description-only skill:

```powershell
& "$env:LOCALAPPDATA\Programs\Secret Handoff\SecretHandoff.exe" --secret API_TOKEN -- 'C:\Tools\Worker.exe' --mode fetch
```

Check standalone help for `Output contract: passthrough`. Older builds discard target output and implement a different lifecycle; do not use them for the current workflow. Current packages include PRODUCT-CONTRACT.md and the skill source folder, but installing the application does not install or advertise the skill. The optional Invoke-Handoff.ps1 is only a JSON compatibility entry point; it adds no caller binding or status banner. See [ARCHITECTURE.md](ARCHITECTURE.md).

## Automated releases

Pushing a tag such as `v0.1.0` automatically builds and publishes a release. Tags such as `v0.1.0-rc.1`, `v0.1.0-alpha.1`, and `v0.1.0-beta.1` create prereleases. The workflow first runs synthetic integration tests, then builds the installer/ZIP, checks the packaged EXE and installer behavior, and publishes only if these steps succeed. Invalid `v*` tags fail. Reruns do not overwrite existing releases. Creating the release workflow does not itself create a version tag.

The following commands publish a release and should be run only when a release is intended:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The build job has read-only repository permissions. Only the subsequent publishing job receives `contents: write`. It uses the automatically supplied `GITHUB_TOKEN`; no separate credentials are needed. Integration tests use only synthetic local processes. Packaging contacts official tool/package sources, not secret providers. GitHub Actions are pinned to commit SHAs; Dependabot is configured to propose action updates.

The separate [Windows CI workflow](https://github.com/JustUndef/secret-handoff/blob/main/.github/workflows/ci.yml) builds and runs the synthetic suite on pull requests and pushes to `main`. It has no release-publishing job. A new workflow or dependency update still needs a successful run before its behavior can be claimed as verified.

## Local packaging

On GitHub Actions, the release workflow can also be started manually using **Run workflow** with a test version. This performs the same build, package, and installer checks and uploads the packages as a workflow artifact, without publishing a release.

Prerequisites: the .NET SDK selected by `global.json` and an existing Inno Setup 6 installation. The build scripts install no tools globally. See [CONTRIBUTING.md](CONTRIBUTING.md) for source checkout setup.

```powershell
.\scripts\Build-Prototype.ps1
.\scripts\Build-Release.ps1 -Version 0.1.0
# RELEASE_READY identifies the new output directory.
.\scripts\Test-Package.ps1 -AppDirectory '<build-base-directory>\app'
```

Use `-DotnetPath` or `-IsccPath` for explicit tool paths. Without an explicit SDK path, the build scripts look under the user profile, then on PATH. `Build-Prototype.ps1` also accepts `-DotnetPath`. The release script downloads official runtime packages from NuGet only for release packaging; prototype builds use no external package sources. Each package build uses a fresh directory under `artifacts/release-builds`, preventing stale files from being included. Packages contain the program, shared wrapper, and public documentation, including agent/contributor guidance; they exclude demo requests, test programs, and generated reports from `artifacts`.

The bundled runtime is pinned to **10.0.14** in `release-runtime.json`, checked on October 28, 2026 against the [official .NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). This prevents the older development SDK from determining the shipped runtime version. The package test checks both included framework versions. Runtime, Desktop, and host license/notice files are copied verbatim from those restored packages into `third-party/`; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Review runtime security updates, update the pin, and build and install new packages as needed. The bundled runtime receives no independent updates from the launcher; there is no automatic updater. References: [.NET publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/) and [Inno Setup: per-user installation](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm).
