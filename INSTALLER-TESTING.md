# Installer testing

## Scope

`scripts/Test-Installer.ps1` checks silent installation, reinstallation of the same version, launch of the installed EXE, and uninstallation. The PATH checkbox remains unchecked on a new installation.

`scripts/Test-InstallerPath.ps1` compiles the same installer with a registry subkey overridden only at build time. All PATH data resides under a new `HKCU\Software\SecretHandoff\PathTests\<random-ID>\Environment`; the real user and system PATH are neither replaced nor printed. The shipped installer has no runtime switch for redirecting the registry. The test package is created outside the release files and is not published. Its temporary installation and registry test area are removed afterward. Installer tests refuse to run if Secret Handoff is already installed.

Tests cover the unchecked default, adding an entry, reinstalling without duplication, preserving subsequently added unrelated entries, pre-existing unmanaged entries with different capitalization/quotes/trailing backslashes, explicit deselection, both registry string types, and originally missing or empty PATH values. Variable references and empty PATH segments are synthetic test data and must remain unchanged.

## Reproduction

```powershell
.\scripts\Test-Installer.ps1 -InstallerPath '<release-directory>\SecretHandoff-<version>-win-x64-Setup.exe'
.\scripts\Test-InstallerPath.ps1 -PublishDirectory '<build-base-directory>\app'
```

An existing Inno Setup 6 installation is required; alternatively, specify its compiler using `-IsccPath`. Both checks run in the release workflow before artifact upload.

## Evidence

Executed locally on Windows on October 28, 2026: Inno compilation succeeded, and all six synthetic PATH test groups passed with `INSTALLER_PATH_TESTS_PASSED`. This used the last previously verified release-capable program build; the CLI extension being developed in parallel was neither built nor modified. An initial run showed that the test also needed to wait for the temporary uninstall helper process. Tests now wait for its process tree. Ownership metadata is stored in a separate installer registry key so that it survives reinstallation.

The normal local installer round trip subsequently passed as well. The [GitHub test run for commit 792b863](https://github.com/JustUndef/secret-handoff/actions/runs/40716000388) passed completely: the existing synthetic integration tests, release packaging, package verification, normal installer round trip, isolated PATH tests, and artifact upload. Publishing was skipped as intended for the manual run; no version tag or release was created.

Manual checkbox interaction and propagation of changes through different terminal/Codex launch paths require separate checks. An already running application does not receive a new process environment block from the installer. Concurrent independent writes by other programs to the same registry PATH are not coordinated transactionally.
