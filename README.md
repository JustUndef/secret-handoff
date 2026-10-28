# Secret Handoff

A Windows prototype for explicitly handing secrets to **newly launched programs and agent tasks** across projects.

The user works in Codex Desktop. A local window displays the program, arguments, working directory, and required secret names. The user pastes values from their password manager into masked fields and explicitly approves the launch. The launcher passes these values only through the target process's environment; its descendants can inherit them.

## Status as of October 28, 2026

**Runnable prototype for synthetic testing; full MVP acceptance is still pending.** Direct CLI requests without a JSON file, JSON requests, the approval dialog, a minimal target environment, direct process creation, Job Object cleanup, and caller process monitoring are implemented. Four review findings have been addressed: unambiguous test process ownership, cleanup after UI failures, independently verified output suppression, and local path validation. See [TESTING.md](TESTING.md) for executed checks and outstanding criteria. The source code is available under the [MIT License](LICENSE). Installers, portable packages, and the automated release build are described in [RELEASING.md](RELEASING.md).

An earlier project tested a limited PowerShell/WinForms dialog that passed values to a child process, as well as a synthetic `op run --environment` invocation. Those results were reported in the project brief; only the two approved general reference scripts were read here, without executing or copying them. They do not establish that this launcher is hardened.

## Implemented prototype

- One general-purpose Windows program, with direct CLI metadata or an optional JSON request containing no secret values; no project-specific handoff scripts.
- Masked local input with a Paste button and Ctrl+V; explicit **Start** or **Cancel** actions.
- No secret values in chat, arguments, files, logs, telemetry, global environment variables, or an additional vault.
- A freshly constructed environment block, direct process creation, and a managed process lifecycle.
- No forwarding of target stdout/stderr to Codex; results use fixed status categories and exit codes.
- When invoked through the shared wrapper script, the launcher also terminates its task if the monitored caller is forcibly terminated and cannot perform its own cleanup.

Technology: **C# with WinForms on .NET 10 LTS**, initially targeting Windows 11 x64. The encapsulated Win32 process adapter assigns the target to a Windows Job Object at process creation. Development builds use the SDK selected by [global.json](https://github.com/JustUndef/secret-handoff/blob/main/global.json) and require an installed .NET 10 Desktop Runtime. Release packages include the .NET runtime; see [RELEASING.md](RELEASING.md). LTS reference: [Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Try it locally

From a source checkout, build the demo using the command below; generated demo files are not committed to Git. After building, double-click `artifacts\demo\Start-Synthetic-Demo.cmd`. Use only these public synthetic test values in the dialog:

- `EXAMPLE_API_TOKEN`: `synthetic-demo-one`
- `SECOND_SECRET`: `synthetic-demo-two`

Review the invocation, paste the values locally, and select **Start**. Expected result: `COMPLETED`, target exit code 0. Other values produce `TARGET_FAILED`, target exit code 49. Cancelling before launch creates no probe process. The real Windows clipboard and physical keyboard interaction still require manual verification; automated tests use a separate test clipboard.

Regenerate the demo after moving the project folder, because its request file contains absolute paths. To rebuild and test, run this from PowerShell in the project folder:

```powershell
.\scripts\Build-Prototype.ps1
```

Prerequisites are Windows 11 x64, PowerShell 7.3 or later, and the SDK selected by `global.json`. The script uses an SDK under the user profile or on PATH; use `-DotnetPath` to select another existing installation. It builds without external packages and places the test version in `artifacts\demo`. It changes neither the global PATH nor the PowerShell execution policy. Opening the generated demo requires no shell preparation. Full contributor setup: [CONTRIBUTING.md](CONTRIBUTING.md).

The JSON invocation remains `SecretHandoff.exe --request <absolute JSON path>`. A reusable [wrapper script](https://github.com/JustUndef/secret-handoff/blob/main/scripts/Invoke-Handoff.ps1) waits for the executable and returns only `HANDOFF_EXIT_<Code>`. Example using a request file containing no secret values:

```powershell
.\scripts\Invoke-Handoff.ps1 -RequestPath 'D:\Projects\Example\task.handoff.json'
```

The script automatically binds the launcher to its own process using its ID and Windows creation time. If that process exits, any pending approval is discarded or the running process tree is terminated. The manual demo and direct EXE invocations without this binding remain independent of the caller. The binding controls the lifecycle; it does not prove authorization. Ending only a Codex task while the bound process remains alive does not itself trigger cancellation; this specific desktop workflow still needs verification.

The JSON structure and deliberately restricted target environment are documented in [ARCHITECTURE.md](ARCHITECTURE.md). Target stdin is closed or reads from `NUL`; interactive CLI sessions are not supported.

The request file, program, and working directory must reside on an available local drive. Network drives, SUBST aliases, and reparse points in path components (such as junctions and symbolic links) are rejected. If a sandbox prevents the required Windows queries, launch is also rejected; there is no unvalidated fallback launch. The current integration tests were therefore run outside the Codex sandbox after a successful build.

## Direct CLI requests and help

Use the newly built EXE by its explicit path; no installation or PATH changes are required. In PowerShell:

```powershell
& '.\artifacts\demo\SecretHandoff.exe' --help
& '.\artifacts\demo\SecretHandoff.exe' --secret API_TOKEN -- 'C:\Tools\Worker.exe' --mode check
$LASTEXITCODE

& '.\artifacts\demo\SecretHandoff.exe' --secret API_TOKEN --description 'API for this task' --secret SECOND_SECRET --description 'Second required value' --working-directory 'D:\Projects\Example' -- 'C:\Tools\Worker.exe' --mode check

& '.\artifacts\demo\SecretHandoff.exe' --secret API_TOKEN --working-directory '.\Subfolder' -- 'C:\Tools\Worker.exe' --help
```

The target paths above are illustrative and must exist locally. Every item after the first `--` belongs to the target: in the last example, `--help` is a target argument. Program and arguments remain separate; the launcher does not evaluate a shell or search PATH. The direct mode creates its request in memory and writes no temporary JSON file. Secret **values are entered only in the local masked dialog**, after checking the complete preview; there are no value options, automatic approval, or reuse of earlier input.

The working directory defaults to the caller's current working directory captured at invocation, **not** the EXE's installation folder. Relative `--working-directory` paths resolve against that same captured directory. The resulting absolute existing local directory is validated, displayed in full, and used unchanged for launch. Empty, drive-relative (`C:folder`), root-relative (`\folder`), missing, and otherwise disallowed directories are rejected. Moving between folders after the preview does not change the request. Existing JSON requests still require their own absolute `workingDirectory` field.

| Option | Meaning and default |
| --- | --- |
| No arguments | Open the existing local JSON file picker; no prepared terminal required. |
| `--help` | Standalone only. Write help to stdout, exit 0, no request or approval dialog; works while another request is active. |
| `--request PATH` | JSON mode, once; existing absolute local file path. The JSON contract is unchanged. |
| `--secret NAME` | Direct mode; repeat for 1–16 required secrets. Name only, never a value. |
| `--description TEXT` | Optional, once immediately after its `--secret NAME`; defaults to empty. Empty text is allowed; at most 512 characters, no control/format characters. |
| `--working-directory DIR` | Direct mode only, once; absolute or ordinary relative local directory. Defaults to captured caller CWD. |
| `--watch-parent PID FILETIME` | Optional, once in either mode before `--`. Positive process ID (not the launcher's own ID) and positive decimal Windows creation time, UTC FILETIME. End of that exact process cancels approval or stops the managed process tree. No binding by default. |
| `--` | Required in direct mode, followed by an absolute local EXE path and zero or more individual target arguments. Ends all launcher option parsing. |

Options have exact, case-sensitive spelling; no short options or `--option=value` forms. Option values must not start with `--`. Option order is otherwise free, except that descriptions follow their secret immediately. JSON mode cannot be combined with secrets, descriptions, working-directory options, or a target separator. Missing values, duplicate single-use options, unknown options, invalid names/paths, and incomplete direct requests produce code 5 and a short fixed stderr diagnostic, without echoing supplied metadata or raw exceptions.

Names follow `[A-Za-z_][A-Za-z0-9_]*`, at most 128 characters; case-insensitive duplicates and reserved environment/runtime names are rejected. There may be at most 128 target arguments, with a total encoded target command line of at most 32,767 UTF-16 code units including its terminating NUL. Arguments may be empty and may contain quotes or Unicode; NUL is forbidden. The existing local-drive/reparse-point policy and environment-name denylist apply to both modes; see [ARCHITECTURE.md](ARCHITECTURE.md). Limits remain 5 minutes for approval, 15 minutes for execution, and 5 seconds for cleanup.

## Terminal waiting, exit codes, and caller lifetime

The EXE now uses the **console subsystem**, with WinForms still providing the approval UI. Invoke it directly in PowerShell or cmd to receive help, wait for completion, and read the exit code. `WinExe` was replaced because GUI subsystem programs do not reliably make both shells wait. A `consoleAllocationPolicy=detached` manifest preserves desktop use without a newly allocated console on Windows 11 24H2 and later; older Windows versions may show an additional console window. See [Microsoft's console allocation policy](https://learn.microsoft.com/en-us/windows/console/console-allocation-policy). Older Windows desktop behavior has not been practically verified.

In cmd, from the project folder (read `%ERRORLEVEL%` on the next line, outside a parenthesized block):

```bat
"artifacts\demo\SecretHandoff.exe" --help
echo %ERRORLEVEL%
"artifacts\demo\SecretHandoff.exe" --secret API_TOKEN --working-directory "D:\Projects\Example" -- "C:\Tools\Worker.exe" --mode check
echo %ERRORLEVEL%
```

In PowerShell, read `$LASTEXITCODE` immediately after the direct invocation. Do not use `start` or `Start-Process` without waiting when you need completion and exit status. The launcher stays alive until the local result window is closed; waiting includes that window after target cleanup. Successful regular requests produce no terminal output. Target stdout/stderr still go to `NUL`; only fixed launcher help and configuration/error diagnostics use the terminal.

Your shell applies its own quoting before the launcher receives arguments. In PowerShell, use single quotes for literal metadata. Empty arguments and embedded quotes require modern native argument passing (PowerShell 7.3 or later, default Windows/Standard mode) or `ProcessStartInfo.ArgumentList`; Windows PowerShell 5.1's legacy argument conversion cannot preserve every such argument. In cmd, quote spaces and escape shell metacharacters/percent expansion according to cmd rules. The launcher does not undo shell expansions and does not substitute `%NAME%`, `$env:NAME`, or `${NAME}` itself.

For a directly bound agent request in PowerShell, supply lifetime metadata explicitly:

```powershell
$owner = [Diagnostics.Process]::GetCurrentProcess()
try {
    $created = $owner.StartTime.ToUniversalTime().ToFileTimeUtc()
    & '.\artifacts\demo\SecretHandoff.exe' --watch-parent $PID $created --secret API_TOKEN -- 'C:\Tools\Worker.exe' --mode check
    $handoffExit = $LASTEXITCODE
} finally { $owner.Dispose() }
```

The existing JSON wrapper sets this binding automatically. A direct invocation without `--watch-parent` remains independent. The binding checks a held process handle and creation time, not account authorization. Agent cancellation is detected only if the launcher or the bound process ends. Stopping a wait or cancelling a Codex task while both remain alive does not trigger cancellation; the actual Codex Desktop cancel button remains an open integration check.

| Exit code | Category | Meaning |
| --- | --- | --- |
| 0 | `COMPLETED` | Target exit 0 and confirmed cleanup; also successful `--help`. |
| 2 | `CANCELLED` | User cancellation or observed caller termination. |
| 3 | `TIMED_OUT` | Approval or execution timeout. |
| 4 | `BUSY` | Another request is active. |
| 5 | `INVALID_REQUEST` | Invalid CLI, request, path, secret name, or caller timestamp mismatch. |
| 6 | `START_FAILED` | Managed launch or caller monitoring could not be established. |
| 7 | `TARGET_FAILED` | Nonzero target exit, with confirmed cleanup. |
| 8 | `INTERNAL_ERROR` | Internal failure. |
| 9 | `CLEANUP_FAILED` | Termination not fully confirmed; takes precedence. |

The numeric target exit code is displayed locally and is not passed through as the launcher exit code. Implemented and executed checks are distinguished from outstanding evidence in [TESTING.md](TESTING.md).

## Security boundary

The tool aims to prevent accidental persistent storage and disclosure in chat. It **does not isolate secrets from sufficiently privileged processes running as the same Windows user**. Approved target code can read, output, store, and transmit secrets. A displayed name or path does not prove identity or authorization. Process termination does not revoke tokens; immediate, complete memory erasure is not guaranteed. The clipboard, operating system dumps, and password manager remain outside the launcher's control.

## Outstanding work

Outstanding items include manual interaction testing, testing the actual Codex Desktop cancel button, full verification of all MVP criteria, and full package acceptance on a clean Windows system. Ctrl+C in a Codex tool process was tested separately on October 27; forcible termination of the shared PowerShell caller with a synthetic target and grandchild process was additionally tested on October 28. These results do not automatically establish the behavior of other cancellation paths. See [TESTING.md](TESTING.md) for next steps and limits of the evidence.

## Contributing and project guidance

Start with [CONTRIBUTING.md](CONTRIBUTING.md) for setup, tests, and pull requests. [AGENTS.md](AGENTS.md) contains shared development and safety rules for coding agents. Required reusable agent artifacts belong in the repository; personal preferences and chat history remain outside it. Participation follows the [code of conduct](CODE_OF_CONDUCT.md), and security reports follow [SECURITY.md](SECURITY.md).

See [CHANGELOG.md](CHANGELOG.md) for unreleased changes and [OPEN-SOURCE-READINESS.md](OPEN-SOURCE-READINESS.md) for the repository review and outstanding release-readiness items. The project uses the [MIT License](LICENSE); bundled components retain their [upstream notices](THIRD-PARTY-NOTICES.md).
