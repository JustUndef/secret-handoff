> HISTORICAL RECORD: superseded on October 29, 2026. This is not current guidance or acceptance criteria. See ../../PRODUCT-CONTRACT.md and ../../TESTING.md. Links below retain their historical context.

# Architecture and MVP

As of October 28, 2026. **MVP specification with a synthetic prototype, not hardened security software.** The requirements below describe the intended MVP. Implemented behavior, executed checks, and outstanding evidence are documented in [TESTING.md](TESTING.md).

Caller discovery and description-only agent integration were added on October 29, 2026; their contract is described below. Full MVP and actual harness cancellation acceptance remain pending.

## 1. Background and evidence

The user works in Codex Desktop and keeps private passwords in a password manager. A general local launcher should accept the values needed for each request, display the target invocation, and start it after manual approval. A separate handoff script should not be necessary for every project.

| Item | Evidence | Limit of the evidence |
| --- | --- | --- |
| PowerShell/WinForms reference | The project brief reported successful local input testing from Codex Desktop; an identity invocation confirmed `SERVICE_ACCOUNT` | A fixed individual invocation, not a general launcher; no private vault contents or contacts retrieved |
| Synthetic environment test | The project brief reported successful handoff through `op run --environment` to a Node probe | Environment selection does not limit the logged-in account's permissions |
| Reference files during the documentation phase | Only the two explicitly approved general scripts were read; nothing was executed or copied | No renewed account access |
| New workspace | Initially empty, with no AGENTS.md in the workspace or checked parent directories and no Git repository | Now has its own prototype and synthetic tests; no private history imported |
| Local development tools at initial verification | The PATH host under `C:\Program Files\dotnet` reported .NET 7, 8, and 9 SDKs. A separately checked host under the user profile reported SDK **10.0.203**. .NET 10 Desktop Runtime versions were **10.0.13** under Program Files and **10.0.7** under the user profile | The prototype was successfully built and tested using the user-profile SDK; these versions describe that workstation, not contributor prerequisites |

The reference demonstrated usable UI components. It trims input, reads all target output into memory, and terminates only the direct process on timeout. Those properties are not adopted as general MVP rules. A successful individual test does not demonstrate hardening.

Earlier product evaluations are not repeated. Service accounts limit permissions but do not replace the desired manual dialog. Another permanent store for their token was rejected. Summon, Keygrant, KeePass/KPScript, and Pinentry offer potential components; a fully verified combination for this workflow is not established. This does not imply that no alternatives exist.

## 2. Goal and MVP boundary

A portable Windows application opens a local dialog and launches a **new** process with one-time secret input supplied as environment variables. The user approves exactly the metadata displayed. The program is neither a password manager nor a provider's authorization broker.

Included: request validation, complete preview, masked input, paste actions, explicit launch, cancellation, fixed timeouts, prevention of concurrent requests, status codes, process-tree cleanup, and synthetic tests.

Excluded: password-manager API integration, service access in the core, background service, web server, remote IPC, modifications to existing desktop processes, automatic approval of agent tools, UAC elevation, interactive terminal sessions, persistent request storage, and a secret vault. A target may open its own GUI window. Programs that only activate an existing process or detach through services are not supported managed tasks. A password-manager CLI could later be launched like any other explicitly approved target program.

## 3. Technology decision

**C#, WinForms, .NET 10 LTS; initial supported platform: Windows 11 x64.** Native controls and clipboard access suit the small Windows dialog. Typed requests and a separately testable lifecycle avoid the growing error surface of a general PowerShell launcher. WinForms is a Windows desktop framework; .NET 10 is the chosen LTS line. The decision was checked on October 27, 2026 against the [WinForms documentation](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/) and [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Release packaging produces a self-contained extractable application and a per-user installer without requiring a globally installed .NET runtime; see [RELEASING.md](RELEASING.md). Packages must be rebuilt for runtime security updates. The development prototype is framework-dependent and uses an installed Desktop Runtime. The subsequently requested installer extends the original MVP scope; an automatic updater remains excluded. Build scripts accept an explicit SDK path and otherwise look under the user profile, then on PATH. No global PATH change or tool installation is performed. The SDK version is selected by `global.json`; contributor setup is documented in [CONTRIBUTING.md](CONTRIBUTING.md).

The EXE contains WinForms input and lifecycle handling and now uses **`OutputType=Exe` with `DisableWinExeOutputInference=true`**, replacing `WinExe`. The actual apphost has PE subsystem `WINDOWS_CUI` (3). PowerShell and cmd can invoke it directly, wait for completion, and receive its exit code. `--help` runs before WinForms initialization, the mutex, and the request dialog, writes fixed help to stdout, and exits 0. Invalid CLI invocations return fixed stderr diagnostics and code 5, with no raw arguments, exceptions, or modal error dialog. Normal requests remain silent on stdout/stderr; the target still receives only the existing `NUL` handles.

The manifest sets `consoleAllocationPolicy=detached`: on Windows 11 24H2 and later, Explorer launch does not allocate a new console, while an existing console remains usable. Older Windows versions may display an additional console window; this variant has not been practically verified. The GUI workflow still needs no prepared terminal. Reference: [Microsoft: Console Allocation Policy](https://learn.microsoft.com/en-us/windows/console/console-allocation-policy). With no arguments, a local file picker opens without saving history. The shared JSON wrapper still waits and adds caller binding. No integration into the environment of already running Codex processes is promised.

Ordinary agent use is **agent harness → SecretHandoff.exe → approved target process**. The installed EXE accepts the complete direct request and optional `--watch-caller`; no application-specific PowerShell module, generated script, or temporary request file is required. Build/test/packaging PowerShell scripts remain development tooling. The optional older JSON wrapper remains compatible and supplies explicit caller binding.

With `--watch-caller`, the launcher queries only its own Windows-recorded immediate parent via `NtQueryInformationProcess(ProcessBasicInformation)` and its own creation time. It opens that parent with non-inheritable `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION` rights, checks the held handle's process ID and creation time, and requires parent creation strictly before launcher creation. A parent PID reused after launcher creation, equal/ambiguous timestamps, invalid metadata, unavailable native entry point, query errors, and access denial fail closed with `START_FAILED`. A missing/exited parent produces `CANCELLED`; a parent that exits after opening is detected through that same retained handle. There is no reopen-by-PID after verification, ancestor search, trust-by-name, or fallback to unbound launch. `NtQueryInformationProcess` is resolved by P/Invoke at first use; absence or incompatible results fail closed. Reference: [Microsoft process information contract and compatibility caveat](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryinformationprocess).

The retained `--watch-parent PID FILETIME` interface accepts caller-supplied positive process ID and UTC Windows creation time. It compares the expected creation time against a held process handle; mismatch returns `INVALID_REQUEST`. Both modes monitor a process lifetime, not account authorization or the agent task itself. A Windows parent substituted through privileged process-creation attributes is not evidence of a true calling agent. No process memory, credentials, or account configuration is read. References: [OpenProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess), [GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes), and [process synchronization objects](https://learn.microsoft.com/en-us/windows/win32/sync/synchronization-objects).

A background task observes the handle with bounded 50 ms waits. Detected process termination or a wait failure cancels the request. The process runner's cancellation token is linked directly to this watch, so cleanup starts independently of the UI event loop. Before approval, the dialog closes without launching a target; during execution, it closes automatically after cleanup. Side effects already performed cannot be undone by simultaneous detection of caller termination. Disposing the watch stops its task before closing the handle.

Missing or already terminated callers produce `CANCELLED` without target launch; a mismatched timestamp produces `INVALID_REQUEST`; insufficient monitoring rights produce `START_FAILED`. Bound invocations return startup/configuration failures through their exit code without leaving an orphaned modal error dialog. The JSON format is unchanged and contains no process binding.

The caller contract remains: cancelling a task must terminate either the launcher directly or the monitored process. Use automatic discovery only if the immediate parent lives for the task and its termination represents cancellation. A persistent agent process that survives task cancellation or an intermediary that exits immediately after spawning is not a supported task owner for that mode. If waiting merely stops while both processes remain alive, the launcher cannot detect task cancellation. A harness may instead retain explicit binding to its known task owner or manage the launcher lifetime directly. Actual desktop cancel-button behavior requires separate verification; there is no universal harness-compatibility claim. Manual invocation without either option remains independent and is cancelled locally.

### Retired skill helper responsibilities

| Former responsibility | Owner in the simplified integration |
| --- | --- |
| Locate an installation and verify supported workflow | Skill instructions: documented explicit default/custom path and standalone help; no PATH search, exact help hash, or new version-query option |
| Create direct metadata and preserve argument boundaries | Existing EXE parser and target encoder; harness supplies separate strings using its ordinary execution API/shell |
| Capture supervising PID/FILETIME | Small EXE enhancement: `--watch-caller` discovers only the immediate parent and retains its verified handle; explicit binding remains |
| Wait and interpret results | Harness waits for the EXE; skill describes unchanged exit codes and local result dismissal |
| Detect caller exit, cancel, time out, and clean up | Existing EXE watcher, approval lifecycle, and Job Object runner; no helper cleanup implementation or background service |
| Suppress target output and restrict target environment | Existing EXE runner, unchanged |
| Report missing installation, permission denial, unsupported workflows | Skill instructions and host's normal approval mechanism; no extra numeric application exit codes |

The shipped skill module and its stand-in test project are retired. The reusable skill contains only Markdown instructions/references and discovery policy metadata. Synthetic integration tests exercise the actual EXE; development scripts may validate the skill files separately.

| Component | Responsibility |
| --- | --- |
| RequestLoader / Validator | Read bounded JSON, validate paths and names, create an immutable request |
| LaunchOptions / CliHelp | Parse CLI options before the first `--`, capture caller CWD, create a direct snapshot or output help |
| LocalPathPolicy | Check the local drive and every canonical path component; reject network drives, DOS aliases, and reparse points |
| ApprovalForm | Complete preview, local input on the STA thread, launch/cancel events |
| ApprovalLifecycle | Own single execution and cancellation; await runner and cleanup independently of UI continuations |
| CallerProcessSource / ParentProcessWatch | Discover the immediate caller when requested, verify/retain its handle, cancel on termination, release handle and task |
| EnvironmentPolicy | Construct a new environment block from the fixed base and entered values |
| WindowsProcessRunner | Win32 launch, restricted handle inheritance, Job Object, waiting and termination |
| SyntheticProbe / Tests | Local probe processes without network/account access; synthetic values only |

There is no general plugin system or local agent server. The small Win32 adapter is necessary to assign the job during process creation. An unprotected `Process.Start()` followed by later assignment would be insufficient for the required cancellation/crash behavior.

## 4. Request contract

### Direct CLI input (implemented)

```text
SecretHandoff.exe --secret NAME [--description TEXT] [--secret NAME ...]
  [--working-directory DIRECTORY] [--watch-caller | --watch-parent PID FILETIME] -- ABSOLUTE_PROGRAM.exe [ARGUMENTS...]
SecretHandoff.exe --request ABSOLUTE_JSON_PATH [--watch-caller | --watch-parent PID FILETIME]
SecretHandoff.exe --help
SecretHandoff.exe
```

`--secret` declares names only, never values. Between 1 and 16 names are required. An optional `--description` must immediately follow its `--secret NAME`, at most once; the default description is empty. Other option ordering is free. Options are exact and case-sensitive, without short forms or `--option=value`. Option values must not begin with `--`. `--request`, `--working-directory`, and `--watch-parent` cannot be repeated. `--help` is valid only by itself; no arguments opens the file picker. File-mode options cannot be combined with direct-mode options or a target separator. Missing values, unknown options, or a missing target/secret are rejected before a dialog opens.

Everything after the first `--` is passed unchanged as the target program and argument list, including empty arguments, additional separators, and options resembling launcher options. The program remains an absolute local EXE path; no shell or PATH lookup. The invoking shell processes its own quoting before passing arguments. For empty arguments and embedded quotes from PowerShell, use modern native argument passing (PowerShell 7.3 or later) or a process API with `ArgumentList`; the launcher cannot undo legacy shell quoting.

The parser captures `Environment.CurrentDirectory` on entry. Without `--working-directory`, this captured caller directory is used. Ordinary relative paths are resolved against exactly this snapshot; absolute paths are checked directly. Empty, drive-relative (`C:folder`), and root-relative (`\folder`) paths are invalid. The existing path policy checks the resulting path, including dot segments, and creates an absolute canonical snapshot. There is no fallback to the installation directory and no environment-variable expansion.

`RequestLoader.Create` creates the direct immutable `LaunchRequest` with the fixed source label `(Direct invocation)`. No temporary file is written. `RequestLoader.Load` continues to load the unchanged JSON contract. Both use `LaunchRequest.Revalidate`, the same path/environment policies, approval dialog, `ApprovalLifecycle`, `ParentProcessWatch`, and `WindowsProcessRunner`. The shared validator also checks description length and display characters. The dialog shows the complete absolute working directory; launch and preview use the same snapshot. The runner revalidates paths before launch but does not resolve relative inputs again.

`--watch-caller` takes no arguments; `--watch-parent PID FILETIME` retains its existing explicit identity contract. Select at most one mode, once, before the target separator, in direct or JSON mode. Repeated/mixed modes produce `INVALID_REQUEST`. PID and UTC Windows creation time must be positive decimal numbers; the launcher's own PID is disallowed. Discovery checks, monitoring, and cancellation limitations are in section 3. Direct invocations without either option remain unbound; the existing JSON wrapper still supplies its explicit identity automatically.

Complete options, defaults, examples, and exit codes: [README.md](README.md) and `SecretHandoff.exe --help`. Both input modes retain the fixed 5-minute approval limit, 15-minute execution limit, and 5-second cleanup budget.

### JSON input (unchanged contract)

The UTF-8 JSON file contains only secret names/descriptions, the target program, arguments, and working directory. No values, placeholder substitution, embedded scripts, or arbitrary environment values. Structural example with illustrative paths that are not assumed to exist:

```json
{
  "executable": "C:\\Tools\\ExampleWorker.exe",
  "arguments": ["--mode", "check", "--label", "Example task"],
  "workingDirectory": "D:\\Projects\\Example",
  "secrets": [
    {
      "name": "EXAMPLE_API_TOKEN",
      "description": "API token for this example task"
    }
  ]
}
```

The extension is unrestricted; `.handoff.json` is the recommended convention. Initially, this is the sole contract, without optional extension fields. Validation happens **before** value entry:

- Reject unknown or duplicate JSON keys, incorrect types, and missing required fields. Limits: 64 KiB file, 1–16 secrets, 128 arguments, 128 characters per name, 512 per description. The serialized Windows command line, including terminating NUL, must not exceed 32,767 UTF-16 code units.
- `executable` is an existing absolute local `.exe` path; `workingDirectory` is an existing absolute local directory. The request file also follows the local path policy. No UNC/device paths, PATH lookup, file association, or environment-variable expansion.
- Arguments are individual strings, may be empty, and must never contain secret values or references. The launcher does not substitute `%NAME%`, `$env:NAME`, or `${NAME}`. Reject NUL; display argument control characters as visible escape sequences. Descriptions and paths must not contain misleading control or bidirectional formatting characters.
- Names follow `[A-Za-z_][A-Za-z0-9_]*`; reject case-insensitive duplicates. All secrets are required.
- Metadata may itself be sensitive. Do not automatically transfer values from chat or clipboard into JSON or include raw request files in error logs. The launcher cannot reliably detect a secret accidentally stored in metadata.

Canonical absolute DOS paths on available local fixed, removable, CD-ROM, and RAM drives are supported. Ordinary slashes and dot segments are syntactically normalized before file access; the preview uses the canonical snapshot. Drive type and direct DOS device mapping are checked before attribute/content access. Network drives, unknown drives, and SUBST aliases are rejected. Each canonical component, including the drive root, is checked individually; reparse points including junctions, symbolic links, mounted folders, and corresponding cloud placeholders are excluded. Alternate data streams, device names, wildcards, and ambiguous trailing dots/spaces are also rejected. A failed query rejects the request, including failure caused by a sandbox restriction.

The file is read once. Preview and launch use the same immutable request. The runner revalidates the program and working directory at entry and immediately before `CreateProcessW`, using the same path policy; modified arguments are not silently reread. Rereading requires a new dialog. Validation neither binds file identity nor locks components against replacement. Concurrent changes remain possible between drive checks, component checks, and launch, potentially making another or remote target path reachable. The policy does not isolate against authorized local manipulation.

## 5. Dialog and values

The window displays the absolute request path, program path, each argument separately with its index and unambiguous string representation, working directory, and all secret names/descriptions. The argument list is authoritative; an additional command-line preview is only an overview. Long content remains fully accessible through scrolling rather than only ellipses. Program path and working directory remain visible during input.

Also displayed: the fixed environment policy, output suppression, timeouts, and notice that the target and descendants receive the values and that the launcher provides no program isolation. A display name is not treated as a trusted identity.

Each secret has a masked text field (`UseSystemPasswordChar`), Paste button, and Ctrl+V using the same paste path. No automatic clipboard reading, plaintext toggle, or copy/cut operation. Pasting replaces the selected text exactly once; an unavailable clipboard produces a fixed local message without exposing content. No silent truncation or normalization. Reject NUL and line breaks; preserve other Unicode and leading/trailing spaces. Reject empty values; allow at most 16,384 UTF-16 code units per value, with a fixed error message containing no value for larger input. This is a deliberate MVP limit for single-line secrets.

**Start** is available only when fields are valid and requires explicit mouse or keyboard activation of the button; Enter in an input field does not launch. Escape, Cancel, and closing the window before launch discard the request. Double-clicks or concurrent events must create at most one process.

After launch, clear fields and temporary value references as early as possible. A status window remains open with **Stop task**; closing it also stops the task. No value reuse after restart or failure. The clipboard is neither stored nor automatically cleared: clearing cannot reliably remove history and could overwrite newer user content. Clipboard history, synchronization, and password-manager behavior remain outside the tool's control.

`ApprovalLifecycle` owns the runner task and cancellation source independently of the WinForms event loop. Disposing the form first cancels and awaits runner cleanup without waiting for a UI continuation. Even after an `Application.Run` exception, a fixed error report appears only after that step. `CLEANUP_FAILED` takes precedence over a UI error; a failed error dialog cannot overwrite it. Late UI continuations do not access disposed controls.

## 6. Environment, arguments, and process creation

The target environment block is constructed anew, **not copied from the parent process**. Fixed minimal base: `SystemRoot` and `WINDIR` from the Windows directory through an OS API; `PATH` contains only that directory's System32 and Windows directories. Only confirmed secret names/values are added. No inherited provider tokens, search paths, or runtime options, and no inheritance fallback. Programs requiring additional variables may be incompatible with this MVP; a future extension needs its own reviewed policy. User/system variables are not changed. Secrets already present in the caller are not retroactively removed.

Secret names are also checked case-insensitively against a versioned denylist. Initial list: `PATH`, `PATHEXT`, `SystemRoot`, `WINDIR`, `COMSPEC`, `TEMP`, `TMP`, `HOME`, `USERPROFILE`, `APPDATA`, `LOCALAPPDATA`, `PSModulePath`, `ENV`, `BASH_ENV`, `JAVA_TOOL_OPTIONS`, `_JAVA_OPTIONS`, `JDK_JAVA_OPTIONS`; plus prefixes `SHANDOFF_`, `DOTNET_`, `CORECLR_`, `COMPlus_`, `COR_`, `NODE_`, `PYTHON`, `RUBY`, `PERL`, `LD_`, `DYLD_`, `ELECTRON_`, `GIT_`, `SSH_`, and `PROGRAM`. This blocks obvious search-path/runtime controls disguised as secrets. It does not identify every program-specific control variable or protect against approved malicious target code.

Program and argument list remain separate in the model. The adapter uses `CreateProcessW` with explicit `lpApplicationName`, its own Unicode environment block, and a working directory. Windows technically requires a command-line string; it is built in exactly one place from the list using tested Windows/CRT quoting. Do not concatenate shell commands. Empty arguments, spaces, quotes, and backslashes must survive the round trip in .NET/CRT probes. Programs with different custom argument parsers need separate compatibility evidence. References: [CreateProcessW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw) and [CRT argument parsing](https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments?view=msvc-170).

No implicit `cmd.exe /c`, `powershell -Command`, or similar interpreter. `.cmd`, `.bat`, and `.ps1` are not launched directly. An explicitly configured interpreter, such as `pwsh.exe` with `-File` and a script path or `node.exe` with a script path, is an approved target invocation displayed with all arguments. Its own evaluation and script are part of the trust decision. An extension or path provides no security guarantee.

The runner creates an unnamed Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, without breakaway permissions. Assignment happens during `CreateProcessW` through `STARTUPINFOEX` / `PROC_THREAD_ATTRIBUTE_JOB_LIST`; there is no later assignment with an unprotected window. The job handle remains non-inheritable and is passed to neither target nor caller. Only explicit standard I/O handles are inherited through a handle list. Failed job configuration, attribute-list setup, or launch has no unmanaged fallback. Reference: [process creation attributes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).

## 7. Lifecycle, cancellation, and status

States: `Validating → AwaitingApproval → Starting → Running → CleaningUp → Terminal`. Every path after value entry passes through cleanup. No target process is allowed before `Starting`. Cancellation during `Starting` is latched and terminates a newly created job before the task is considered complete. Windows may execute approved target code immediately after process creation; simultaneous cancellation cannot undo completed side effects.

- One request per Windows user and interactive session, enforced by a session-local user-specific mutex. Further requests immediately receive `BUSY`, without an input window, queue, or value reuse. The mutex is not an identity/security boundary against the same user.
- Approval timeout: 5 minutes after showing the dialog. Execution timeout: 15 minutes after successful process creation. Fixed MVP limits; other requirements need a later contract change. No hidden unbounded waiting.
- Cancellation, closing the window, and execution timeout terminate the entire managed job. There is no continue-in-background path. Normal termination of the direct target also terminates remaining job descendants; this is disclosed before launch.
- After a termination event, terminate the job, verify that its process count reaches zero, and close all handles. Cleanup budget: 5 seconds. Without confirmation, return `CLEANUP_FAILED`, never success; close the final job handle anyway. Under normally functioning Windows, no managed processes should remain within that budget.
- On launcher crash, Windows closing its last job handle should terminate managed processes. An enclosing job already present at startup, such as one from the desktop caller, must be considered in integration tests. Incompatible restrictions cause startup failure.
- A bound caller's termination triggers the same cancellation/cleanup path even without the wrapper's `finally`. An open result display closes automatically; an already determined result, especially `CLEANUP_FAILED`, is preserved.
- While no bound caller terminates, the fixed result display remains visible locally until dismissed; no fields or values remain. After job completion, the cleaned-up process may release its mutex. Callers wait for dismissal before receiving the exit code; UI waiting is not target execution time.

Windows Job Objects manage ordinary descendants created through process creation. They are not a sandbox: handoff to existing processes, services, or alternative activation paths is not comprehensively covered. Such requests are unsupported and must not be advertised as preventing orphaned processes under every circumstance. Reference: [Microsoft: Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).

| Exit code | Fixed category | Meaning |
| --- | --- | --- |
| 0 | `COMPLETED` | Direct target exited 0 and job cleanup confirmed; also successful standalone `--help` |
| 2 | `CANCELLED` | User cancellation or detected caller termination; cleanup confirmed if started |
| 3 | `TIMED_OUT` | Approval/execution limit reached; cleanup confirmed if started |
| 4 | `BUSY` | Another request is active |
| 5 | `INVALID_REQUEST` | Invalid CLI, contract, path, or environment name; no target launch |
| 6 | `START_FAILED` | Target launch/job setup or caller monitoring failed; no managed process remains running |
| 7 | `TARGET_FAILED` | Target exited nonzero; cleanup confirmed |
| 8 | `INTERNAL_ERROR` | Internal failure; cleanup confirmed if started |
| 9 | `CLEANUP_FAILED` | Termination not fully confirmed; takes precedence over every other category |

The numeric target exit code may be displayed locally but is not passed through as the launcher exit code. Missing input keeps the dialog open without creating a process; it is not a terminal error. Error text uses fixed application messages and, where applicable, numeric Windows error codes; no raw exceptions or target messages.

## 8. Output and security boundaries

Target stdin reads from `NUL`; stdout/stderr write to `NUL`. No target pipes, output buffers, console inheritance, raw preview, or launcher-created target output files. Even large or binary output cannot create an unbounded capture buffer. The launcher writes only fixed CLI help to stdout and its own fixed configuration/error diagnostics to stderr; normal requests remain silent. No persistent logs or unvalidated target text. Diagnostics use only synthetic probes and structured test results.

This suppression covers only those standard channels. An approved target may use its own windows, files, network connections, telemetry, existing processes, or other output channels. It can transform, forward, or persist secrets. A future output filter would not provide complete security. The tool is not a network filter and does not give the target OS permissions more restricted than the user's.

Further limitations:

- The target and ordinary descendants can read and inherit environment values. Clearing UI fields or launcher references does not remove values already copied by the target.
- Sufficiently privileged malware running as the same Windows user, debuggers, memory dumps, and administrators are outside the protection claim. Process environment is not a protected vault.
- UI controls, .NET strings, clipboard, and native environment blocks may contain copies. Owned mutable buffers are overwritten on a best-effort basis after use and handles are reliably released; immediate complete memory erasure is not guaranteed. No application-created dumps or crash uploads; OS/third-party dumps cannot be ruled out.
- The launcher creates no secret files, registry values, Credential Manager/DPAPI store, or global environment changes. It cannot exclude paging, hibernation, clipboard history, or target-created storage. The separately optional installer PATH integration is described in RELEASING.md and never contains secrets.
- A displayed application name/path is neither account/vault authorization nor proof of unchanged code. Scripts, modules, working directories, file replacement between validation and launch, and local manipulation remain relevant. There is no automatic trust store.
- Process termination ends local provisioning but revokes no token and undoes no external actions already performed.

## 9. Verifiable MVP acceptance criteria

All tests use synthetic values and local probes without real accounts or network access. No secret values in test reports, snapshots, screenshots, or logs. A local probe reports fixed result codes only; the separate output-discard test intentionally writes synthetic values into discarded standard channels. Production I/O policy is used unchanged. Automated GUI checks from Codex tool processes have run; manual interaction checks and the actual desktop cancel button remain outstanding. Evidence: [TESTING.md](TESTING.md).

| ID | Check | Required result |
| --- | --- | --- |
| A01 | Launch from Codex Desktop without a prepared terminal; long target path and multiple secrets | Complete readable preview; no target before approval; every argument accessible |
| A02 | Pass synthetic values including Unicode, leading/trailing spaces, and special characters | Exact target match, no values in argv/request file; no caller/user/system environment changes |
| A03 | Cancel, Escape, window close before launch, and 5-minute approval timeout | No probe/start marker; input discarded; category 2 or 3 |
| A04 | Empty fields; NUL, line break, or oversized value | Understandable fixed local message, no truncation or launch; valid input permits later launch |
| A05 | Paste button and Ctrl+V with selection, empty clipboard, and temporarily locked clipboard | Exactly one masked insertion; no automatic clipboard reading; retry possible after lock |
| A06 | Arguments: empty, spaces, `"`, backslash before `"`/at end, Unicode, and `& \| > % $ ;` | Identical argv round trip; no unexpected shell action, expansion, or extra processes |
| A07 | Invalid JSON, duplicate keys, unknown fields, relative/missing paths, direct script file | Category 5 before value entry; no target; no raw JSON/exception contents in agent context |
| A08 | Case duplicates, `Path`, `NODE_OPTIONS`, other denied names; synthetic credentials in parent | Invalid names rejected; target receives exactly the fixed base and approved names, no inherited credentials/controls |
| A09 | Missing/invalid EXE, denied launch, failed job assignment | Fixed category 5 or 6 by failure phase; no raw errors or unmanaged fallback |
| A10 | Target writes synthetic secrets to stdout/stderr, including binary and large output | Both channels discarded; no values in agent context/launcher files; no buffer growth/deadlock |
| A11 | Target exits 0/nonzero, with/without remaining descendants | Category 0/7; correct local target code; job empty afterward, handles released |
| A12 | Cancel during startup/runtime; timeout; kill launcher during startup/runtime; cancel caller task in Codex Desktop | Under normally functioning Windows, no managed process after at most 5 seconds; status only after cleanup; crash/caller cancellation also terminate job |
| A13 | Concurrent requests, rapid double-click on Start, repeated tasks | Second request gets 4 without dialog; at most one launch per approval; no value reuse or growing handle count |
| A14 | Modify request after preview; launch under enclosing desktop job | Metadata remains the approved snapshot; compatible nesting works, otherwise category 6 |
| A15 | Synthetically inject cleanup failure | Category 9 overrides success/cancellation; no false completion; final job handle closed |
| A16 | Check all success/failure paths for file writes and diagnostic channels | No launcher-persisted secrets, raw logs, or telemetry; no secret values in test artifacts |

For A03/A11/A12, the synthetic probe may write a value-free start marker in a test directory and use known local synchronization events. Descendants/PIDs are tracked under controlled ownership; unrelated processes are never terminated. Timeouts have an internally injectable monotonic clock for fast tests; at least one manual test must cover real durations. A16 is limited evidence for tested paths, not a guarantee about arbitrary target code or the OS.

## 10. First implementation step

**A synthetic vertical slice, not approval for real secrets.** This step is implemented as the first prototype; the following describes its scope. It used the existing .NET 10 SDK without a global installation step.

1. Create a small solution with a WinForms EXE, UI-independent contract/lifecycle classes, isolated Win32 runner, and local synthetic probe. No provider dependency or private reference code is needed.
2. Implement exactly the request contract, fixed environment policy, complete preview, and masked input with both paste paths. Ship only a synthetic probe request as the generated example.
3. Implement direct launch with job assignment at creation, `NUL` handles, and one-time approval. Cancellation before/during launch, termination during execution, and normal target exit must already clean up; no provisional unmanaged launcher.
4. First verify A02, A03, A05, A06, A08, A10, A11, and startup/crash/caller cancellation from A12 with a synthetic child/grandchild. Manually verify local dialog interaction and the caller contract from Codex Desktop. Explicitly list remaining MVP checks.

Review deliverables: source code, a generated value-free example request under `artifacts/demo`, reproducible local test commands, and [test evidence](TESTING.md) distinguishing executed checks from outstanding criteria. Only complete implementation and verification of A01–A16 permits claiming the MVP is satisfied; documented security limitations still apply.
