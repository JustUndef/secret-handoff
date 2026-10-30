# Secret Handoff

A reusable Windows application that gives a user-approved program locally entered secrets and returns the program's useful results to its caller.

An agent supplies the executable, arguments, working directory, and secret names. You enter values in a masked local dialog and approve the invocation. The program receives the values in its environment. Its stdout and stderr return unchanged to the agent, including credentials or data deliberately retrieved by the approved operation.

The recipient may be a CLI, an SDK-based script, or another executable. No provider is built into the launcher. The entered access secret and the returned result are different things; the launcher itself neither prints nor stores the entered secret.

## Current contract

The owner's clarified scope is in [PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md). The October 29, 2026 scope reset replaces mandatory supervision and discarded output. Earlier architecture and test records are retained under [docs/history](docs/history), not as current instructions.

The direct target is awaited without a fixed runtime limit. The window closes automatically when it finishes. Local Stop or closing the running window stops only that direct target. Descendants manage their own lifetime; stopping the agent or terminating the launcher does not guarantee task cancellation. Independent requests may run concurrently.

The receiving program is trusted. It can disclose its input, and its output may contain sensitive data. The launcher is not an isolation boundary against malicious recipients or sufficiently privileged same-user programs. Clipboard history and OS memory copies are outside its control.

## Direct use

Use the installed absolute EXE path or a reviewed portable build. All arguments below are metadata; enter values only in the dialog.

```powershell
& "$env:LOCALAPPDATA\Programs\Secret Handoff\SecretHandoff.exe" --secret API_TOKEN --description 'Access for the approved operation' --working-directory 'D:\Projects\Example' -- 'C:\Tools\Worker.exe' --mode fetch
$LASTEXITCODE
```

For an SDK-based script, use its interpreter's explicit EXE path and script arguments. The launcher does not evaluate shell commands or substitute secrets into arguments. Stdin is NUL; the operation must not require further terminal interaction.

Run standalone `--help` to check the build. The current help contains `Output contract: passthrough`. Older builds discard output and do not implement this workflow. The retired `--watch-caller` and `--watch-parent` options now fail validation rather than silently changing behavior.

Exit codes: 0 completed, 2 locally cancelled, 5 invalid request, 6 start failed, 7 target failed, 8 internal error, 9 direct-target stop unconfirmed. The target's numeric exit code is mapped to 0/7. Target failure output is still returned. No completion banner is added to stdout/stderr.

The target receives ordinary runtime configuration (documented in [ARCHITECTURE.md](ARCHITECTURE.md)) plus the entered values; arbitrary ambient credentials are not copied. Explicit filesystem paths may use ordinary links or mapped drives. Path validation is not proof of trusted code.

## Agent skill

Install the [description-only skill](skills/secret-handoff/SKILL.md) separately following its [setup instructions](skills/secret-handoff/references/setup.md). The EXE contains the application behavior; no runtime module or request file is needed. Skill discovery and application installation are separate.

## Build and test

The core workflow passed human-assisted synthetic handoff and preapproval cancellation checks on October 30, 2026. Masking and recipient visibility were confirmed locally. The revised automated suite and packaged integration tests also passed. Installer/update acceptance remains open; see [TESTING.md](TESTING.md) for evidence and limits. Existing older installations must be updated or replaced by a reviewed portable build before using output passthrough.

Windows 11 x64 and the .NET SDK selected by global.json are the supported development baseline. Release packages include .NET; portable ZIP and per-user installer instructions are in [RELEASING.md](RELEASING.md).

```powershell
.\scripts\Build-Prototype.ps1
.\scripts\Test-Skill.ps1
```

The custom suite uses local synthetic programs and fixed pass/fail reports. `dotnet test` does not run it. Current evidence and outstanding checks are in [TESTING.md](TESTING.md); no real services or accounts are used in development tests.

For a human synthetic test, invoke the built EXE directly with `--secret EXAMPLE_API_TOKEN --secret SECOND_SECRET -- <absolute SyntheticProbe.exe> exchange`. Enter `synthetic-demo-one` and `synthetic-demo-two` in the respective local fields. Expected stdout is `synthetic-result-credential`, stderr is `synthetic-diagnostic`, and launcher exit code is 0. These public fixtures do not represent real secrets.

Optional metadata-only JSON remains supported through `--request ABSOLUTE_PATH`; no arguments opens a file picker. The compatibility script `scripts/Invoke-Handoff.ps1` simply invokes this mode. It is not needed by agents.

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Source is MIT licensed.
