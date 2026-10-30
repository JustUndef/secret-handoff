> HISTORICAL RECORD: superseded on October 29, 2026. This is not current guidance or acceptance criteria. See ../../PRODUCT-CONTRACT.md and ../../TESTING.md. Links below retain their historical context.

# Prototype and test evidence

**Human-assisted Desktop Stop check (October 29, 2026): FAIL for the tested execution path.** An independently started observer verified and retained the live caller, launcher, synthetic target, and child identities. After local human approval, the observer was armed and the user interrupted the active Codex tool wait with Stop. The observer recorded `OBSERVER_STOP_TIMEOUT`; all four processes were still live at the end of its 30-second window. Its subsequent failure cleanup is separate from Desktop cancellation. Follow-up process checks found the recorded launcher, target, and child PIDs absent. No launcher exit status was obtained from the interrupted wait. This result applies to this session's direct `--watch-caller` invocation through the elevated execution tool; it does not establish behavior for other execution paths or harnesses. The earlier observed approval-window closure does not override this running-tree result. The [independent observer procedure](DESKTOP-STOP-TEST.md) remains available for reproduction.

Observer follow-up: the full synthetic suite ran outside the sandbox with **77 PASS, 0 FAIL, `ALL_TESTS_PASSED`**, and the build had **0 warnings and 0 errors**. Six new groups cover a killed caller, a killed launcher with its caller still alive, wrong launcher identity refusal without observer cleanup, armed timeout and verified cleanup, setup timeout, and a normally exited retained handle. Test probe PID markers are atomically published. This does not constitute an actual Desktop Stop test.

As of October 28, 2026. End-to-end prototype with direct CLI input, caller monitoring, and four review fixes; **full MVP acceptance is still pending**. All values and target programs in these checks are synthetic/local. No real secrets or services were used.

## Direct EXE agent integration (October 29, 2026)

**Marker synchronization follow-up:** A later full acceptance run had 68 passes and two `UNEXPECTED` test failures. In 20 focused repetitions, `host-crash-before-resume` reproduced once as a fixed `FORMAT` category: the test could kill the writer after the marker appeared but before its metadata was complete. `cli-caller-exit-before-monitoring` passed all 20 repetitions, so its original exception remains unclassified; its marker used the same unsafe publication pattern. Both test hosts now close a staged metadata file before renaming it to the marker name. A deterministic test checks that the marker is absent during staging and contains complete metadata after publication. Following this change, both affected tests passed 20 repetitions each, and `scripts/Build-Prototype.ps1` completed outside the sandbox with **71 PASS, 0 FAIL, `ALL_TESTS_PASSED`**, and a build with **0 warnings, 0 errors**. These changes affect test code only and do not establish the original cause of the caller test failure.

**Implemented:** `--watch-caller` discovers only the EXE's immediate Windows parent, verifies its identity and creation time through a retained non-inheritable process handle, and fails closed if monitoring cannot be established. It shares the existing cancellation token, approval lifecycle, and Job Object runner. Explicit `--watch-parent PID FILETIME`, JSON mode, and manual/unbound invocations remain supported. The [description-only skill](skills/secret-handoff/SKILL.md) now contains Markdown instructions/references and discovery policy metadata only. The PowerShell skill module, its exact-help-hash requirement, helper-only numeric statuses, and its CLI stand-in project were removed. Build, package, and existing optional JSON-wrapper scripts remain.

**Executed:** `scripts/Build-Prototype.ps1` on Windows 11 x64 with PowerShell 7.6.5 and SDK 10.0.203: **0 warnings, 0 errors; 70 PASS, 0 FAIL, `ALL_TESTS_PASSED`, exit 0** outside the sandbox. A sandboxed CLI run failed on required local Windows metadata queries; validation was not relaxed. An initial new runtime test used the JSON demo's ownership-path expectation for the source-built CLI; the test now verifies its actual source-built EXE and retained handle before manipulating owned windows. No secrets, services, releases, or global user configuration were used or changed.

The full suite includes the previous 60 regression groups and these ten additional groups:

| Test group | Executed evidence |
| --- | --- |
| `cli-caller-options` | Direct/JSON parsing, unchanged unbound default, and target `--watch-caller` after the separator; repeated/mixed modes rejected in the existing CLI rejection group |
| `cli-caller-argument-roundtrip` | Actual EXE with `--watch-caller`, local synthetic approval, exact .NET target argv/environment/CWD, directory with spaces/Unicode, exit 0, and suppressed standard output |
| `cli-caller-cancel-before-approval` | Actual EXE; cancel with empty masked fields, exit 2, no target/grandchild marker |
| `cli-caller-exit-before-approval` | Actual direct EXE launched by an owned synthetic caller; nonrecursive caller termination cancels before target launch |
| `cli-caller-exit-running-tree` | Actual direct EXE; terminate only its owned immediate caller during execution; exit 2 and target/grandchild gone |
| `cli-caller-exit-before-monitoring` | Owned caller creates the actual EXE suspended; test pins/verifies its process and thread, ends the caller, resumes the launcher; exit 2 before any target launch |
| `cli-unbound-survives-caller-exit` | Same direct EXE without either supervision option stays awaiting approval after caller exit; local Cancel exits 2 |
| `cli-caller-normal-end-cleans-descendants` | Actual EXE; normal target exit cleans remaining descendants and returns 0 |
| `caller-discovery-identity-races-and-failures` | Per-instance source seam with owned native handles: missing native API, invalid/self identity, open/query denial, caller gone before open, held-handle ID mismatch, reused/younger PID, ambiguous/equal timestamps, invalid creation time; all rejected with fixed statuses and closed handles; valid held handle observes caller exit |
| `caller-discovery-native-handle-disposal` | Actual Windows immediate-parent discovery, repeated binding/disposal, bounded handle count |

The argument vectors now include `--watch-caller` after the target separator. Existing actual-EXE .NET round trips still preserve empty strings, spaces, quotes, backslashes, Unicode, metacharacters, and launcher-like target options. The source-seam failure/race cases are deterministic simulations, plus an actual query-only owned handle that cannot establish a synchronization wait. They do not claim actual Windows PID reuse, denial when opening a real immediate parent, or a kernel query failure was induced. The suspended-caller test separately exercises an actual pre-monitoring exit through the public EXE interface. No production test flags, automatic approval switch, or validation bypass were added.

Also executed: `scripts/Test-Skill.ps1` reports **2 PASS and `ALL_SKILL_TESTS_PASSED`** for constrained YAML metadata/policy, local Markdown links/fences, and an instruction-only file inventory. It imports no runtime module and builds no stand-in launcher. It needs no external YAML dependency or agent skill; this is a validator for this skill's simple YAML subset, not a general YAML parser. Whitespace/diff checks passed.

**Outstanding manual/harness evidence:** fresh-chat explicit invocation and implicit discovery in a different project, one complete human-entered synthetic handoff through the installed skill, physical clipboard/keyboard/DPI checks, and the actual Codex Desktop cancel button. Automated success uses only owned synthetic test windows; it does not establish manual production approval behavior or universal harness compatibility. Stopping a wait while caller and launcher survive still does not cancel. No release package was rebuilt or published for this change; older installed packages may lack `--watch-caller`. Follow [fresh-chat acceptance](skills/secret-handoff/references/setup.md) with a matching reviewed EXE. Earlier evidence and broader MVP gaps remain below.

## English public project material

Application-owned GUI labels, accessibility text, fixed status/error messages, CLI help, request source labels, installer messages/shortcuts, and synthetic example descriptions now use English. Matching GUI assertions were updated. Unicode fixtures use a neutral non-ASCII character expressed with a C# Unicode escape, preserving argument, value, and path compatibility coverage. The shared agent and contribution rules require English public project material.

Executed after these changes on October 28, 2026: development build with **0 warnings and 0 errors**; full synthetic suite outside the sandbox with **60 PASS, 0 FAIL, `ALL_TESTS_PASSED`, exit 0** (`artifacts/english-tests.txt`). The empty synthetic approval dialog was rendered and visually checked at its default size (`artifacts/english-preview.png`); labels, instructions, inputs, and buttons were readable. Manual clipboard/keyboard and multiple-DPI acceptance remain outstanding.

The self-contained test package rebuilt successfully. Packaged help and invalid-CLI checks, normal install/reinstall/uninstall, and all six isolated PATH test groups passed. Five original component notice files matched the source packages byte-for-byte in the app directory and ZIP, and release-file SHA-256 checks matched. Static language checks found no remaining localized prose in the 59 public source/configuration/documentation files or bundled Markdown documentation. Markdown links/fences and diff whitespace checks passed. These checks created no version tag and published no release.

## Direct CLI input without JSON

**Implemented:** `--secret NAME [--description TEXT]`, multiple secrets, optional `--working-directory`, final target separator `--`, existing `--request`, file picker, and `--watch-parent` in both modes. Both modes share the validator, immutable snapshot, approval dialog, environment policy, and process runner. Direct mode writes no temporary request file. Secret values remain local masked dialog input only. Full contract and defaults: [README.md](README.md), [ARCHITECTURE.md](ARCHITECTURE.md), and `SecretHandoff.exe --help`.

The EXE was changed from `WinExe` to the console subsystem, including disabling automatic WinForms `WinExe` inference. The test reads the apphost's actual PE header and checks subsystem 3. The manifest includes `consoleAllocationPolicy=detached` for GUI use without a newly allocated console on Windows 11 24H2 and later. Process creation, target handle lists, and job cleanup were unchanged. Test helpers now wait for their own visible WinForms controls with stable layout; `WaitForInputIdle` is unsuitable for the console process. Process/window ownership is still checked before sending mutating GUI messages.

**Executed:** `scripts/Build-Prototype.ps1 -SkipTests` using SDK 10.0.203: **0 warnings, 0 errors**. The built test runner then ran outside the sandbox on Windows 11 x64, build 26300, with PowerShell **7.6.5** and local Windows **cmd.exe**: **60 PASS, no FAIL, `ALL_TESTS_PASSED`, exit code 0**. Value-free result log: `artifacts/cli-test-results.txt`. This includes the previous 47 test groups and these 13 new groups:

| Test group | Actually checked |
| --- | --- |
| `cli-direct-snapshot-and-descriptions` | Multiple secrets; explicit, missing, and empty descriptions; snapshot unchanged after input-array/CWD changes; absolute preview; no request file created during parsing; JSON accepts reordered caller option |
| `cli-options-rejected` | Missing/unknown options, file/direct conflicts, duplicate single-use options, misplaced descriptions, missing separator/target/secret, relative EXE, case duplicates, denied names, description/argument limits, and NUL rejected |
| `cli-working-directories` | Absolute directory, relative Unicode directory with spaces, dot segments; empty/missing/nonexistent, file, drive-relative, root-relative, and disallowed paths rejected |
| `cli-help-and-invalid-real-processes` | Actual EXE processes: help without request/dialog, stdout and code 0; invalid invocations/directories with code 5, short stderr diagnosis, no echoed test markers |
| `cli-help-while-busy` | Help and invalid invocations work without acquiring the request mutex even while it is held |
| `cli-console-subsystem` | Actual built apphost is `WINDOWS_CUI`, not `WINDOWS_GUI` |
| `cli-no-arguments-picker-cancel` | Actual EXE launched without arguments or console window; its own file picker found and closed; code 2 and empty standard channels |
| `cli-direct-default-cwd-roundtrip` | Actual direct request without file; caller CWD differs from installation directory; complete preview, explicit synthetic approval, target CWD/environment/arguments check, code 0 |
| `cli-direct-absolute-cwd-roundtrip` | Same complete workflow with explicit absolute working directory |
| `cli-direct-relative-cwd-roundtrip` | Same complete workflow with relative `.` resolved against caller CWD |
| `cli-direct-parent-killed-tree` | Direct launcher bound to synthetic caller; forcible nonrecursive caller termination leads to code 2 and confirmed target/grandchild termination |
| `cli-powershell-help-errors-wait-exits` | Actual PowerShell process: EXE help/code 0, CLI error/code 5, waiting before approval and until result dismissal; success/code 0, target failure/code 7, cancellation/code 2 reach the shell |
| `cli-cmd-help-errors-wait-exits` | Same five real process workflows through cmd, checking help, stderr, waiting, and `%ERRORLEVEL%` |

The three direct round-trip groups check empty arguments, spaces, embedded quotes, backslashes, Unicode, metacharacters, and `--help`, `--secret`, `--request`, `--working-directory`, `--watch-parent`, and `--` **after** the separator. All reach the local target unchanged and are not treated as launcher options. The complete argument matrix uses `ProcessStartInfo.ArgumentList`. Shell groups check ordinary quoted metadata and lifecycle; they do not establish all cmd/PowerShell quoting variants. Windows PowerShell 5.1 and its legacy quoting are not verified by this run.

The same successful run includes earlier regressions: JSON/file mode, minimal environment and credential exclusion, independently captured output suppression with a negative control, startup/runtime cancellation, timeout, launcher crash, target/grandchild cleanup, UI errors, mutex, caller handles/PID reuse, and forcible termination of the actual JSON wrapper. The production EXE has no auto-approval/autofill switch. Synthetic values are written by test helpers only into masked fields of owned test windows, never CLI/JSON/logs.

After a successful build, run the full suite in a context permitting Windows metadata queries:

```powershell
$previousShell = $env:SHANDOFF_TEST_POWERSHELL
try {
    $env:SHANDOFF_TEST_POWERSHELL = (Get-Process -Id $PID).Path
    & '.\tests\SecretHandoff.Tests\bin\Debug\net10.0-windows\SecretHandoff.Tests.exe'
    # Optional: run only the 13 new groups with the same EXE and --cli-tests.
} finally { $env:SHANDOFF_TEST_POWERSHELL = $previousShell }
```

**Outstanding evidence:** the actual Codex Desktop cancel button, manual clipboard/keyboard/DPI interaction, Explorer double-click on different Windows versions, Windows before 24H2, Windows PowerShell 5.1, a native CRT target, and the other MVP variants below. Explicit local approval is still required. Merely ending an agent wait while the launcher and bound process remain alive does not cancel the task. During the CLI implementation task, installers, CI/CD, PATH integration, and release packages were not changed, rebuilt, or published; earlier package reports below are historical evidence. Subsequent repository-readiness changes are documented separately in [OPEN-SOURCE-READINESS.md](OPEN-SOURCE-READINESS.md).

## Reproduction

### Installer and release build evidence

Additional checks on October 28: development rebuild with 0 warnings/errors and all 47 then-existing synthetic groups passing outside the sandbox. A self-contained `win-x64` package version `0.1.0-rc.1` was built locally with official .NET runtime packages; Inno Setup produced the per-user installer. `Test-Package.ps1` reported `PACKAGE_SMOKE_PASSED`. `Test-Installer.ps1` installed into a fresh project test directory, checked the installed EXE, reinstalled, and uninstalled successfully (`INSTALLER_ROUNDTRIP_PASSED`). No execution policy was changed and no real secrets were used. An initial sandbox run failed because metadata queries were blocked; the path policy was not relaxed.

This checks package contents, startup/error status of the packaged EXE, silent installation, same-version reinstallation, and uninstallation. Upgrading an older published version, the manual installer UI, and a system without any preinstalled .NET have not yet been practically checked. SHA-256 checksums demonstrate integrity against a known comparison value, not publisher identity; packages are unsigned. Build/release instructions: [RELEASING.md](RELEASING.md).

The bundled runtime was subsequently pinned independently of the development SDK to **10.0.14**. Local rebuild, both framework-version checks, install/reinstall/uninstall, and SHA-256 file verification passed. The [GitHub Actions run](https://github.com/JustUndef/secret-handoff/actions/runs/40711889604) for commit `43fbe42` passed on `windows-2025`: synthetic integration tests, self-contained packaging, runtime/EXE verification, installer round trip, and artifact upload. Publishing was skipped as intended for this manual run. An actual tag push publishing a GitHub release was not performed; that final path remains practically untested until an intended version tag is pushed. Later isolated PATH-test evidence is in [INSTALLER-TESTING.md](INSTALLER-TESTING.md).

From PowerShell in the project directory:

```powershell
.\scripts\Build-Prototype.ps1
```

The script builds the solution, generates the demo, and executes the custom test runner. It accepts `-DotnetPath`; otherwise it looks for an SDK under the user profile, then on PATH. `global.json` selects the SDK version. No external NuGet packages are used; package sources are cleared for prototype builds. SDK telemetry, development certificate generation, and global tool PATH additions are disabled in the script; temporary process variables are restored afterward. Workload resolution and the persistent compiler server are disabled because of execution-environment restrictions. Do not change system policies. Clean-checkout prerequisites are listed in [CONTRIBUTING.md](CONTRIBUTING.md).

Before the CLI extension, checks on Windows 11 x64 (build 26300), SDK 10.0.203 produced **0 warnings, 0 errors**, followed outside the Codex sandbox by **47 PASS and `ALL_TESTS_PASSED`**. Value-free result log for the review fixes: `artifacts/review-build-and-test.txt`. The older `artifacts/build-and-test.txt` records the previous 32-group state. The 60-group CLI run is described above. These generated local artifacts are intentionally not committed; the reproducible commands, source tests, and evidence summaries are public.

The first integrated sandbox run failed: `QueryDosDeviceW` was blocked with `ERROR_ACCESS_DENIED`, and the new path policy rejected requests. A targeted local check outside the sandbox confirmed a directly mapped local drive. The same built runner then ran fully outside the sandbox with the current PowerShell path in `SHANDOFF_TEST_POWERSHELL`; the temporary process variable was restored afterward. The policy was not relaxed. Reproduction requires a context permitting local Windows metadata queries; no system policies were changed.

The generated demo under `artifacts/demo` is framework-dependent, not a self-contained release. Its JSON contains absolute metadata paths and no secret values. Regenerate it after moving the directory.

## Executed regression tests

| Test group | Evidence |
| --- | --- |
| `request-contract` | JSON snapshot, duplicate/unknown keys, invalid format and oversized files, dangerous names, character/value rules |
| `local-path-native-valid` | Actual local probe/request files, canonical paths, trailing directory slash accepted |
| `local-path-reject-drive-before-attributes` | Simulated remote/unknown/missing drives rejected before attribute/content access |
| `local-path-syntax-before-probe` | UNC, device paths/names, streams, ambiguous names rejected before Windows queries; slash/dot-segment normalization |
| `local-path-components-and-types` | Simulated reparse points at root/intermediate/file stop validation before further components; file types checked |
| `local-path-loader-checks-all-paths` | Policy applies to request, target, and working directory; no attribute query on simulated network drive |
| `local-path-post-preview-revalidation` | Simulated drive change after preview produces category 5 without launch; values cleared, request not reread |
| `local-path-immediate-prestart-revalidation` | Simulated drive/reparse change between initial runner validation and CreateProcess prevents creation |
| `environment-and-arguments` | Two exact synthetic values including Unicode/edge spaces; exactly three base names plus two requested names; .NET argv round trip with empty values, quotes, backslashes, shell metacharacters; working directory |
| `pre-cancel-no-start` | Already-cancelled request creates no marker; value dictionary cleared |
| `cancel-during-suspended-start` | Cancellation after suspended creation but before execution cleans up; no target code/marker |
| `invalid-values-no-start` | Empty, line break, NUL, and oversized values rejected |
| `target-failure` | Target code 37 captured locally as a number and mapped to launcher category 7 |
| `stdout-stderr-flood-discarded` | Actual runner in separate host; synthetic values, 32 MiB output, and binary bytes produce exactly zero bytes externally on both channels; fixed drain buffers and timeouts |
| `stdout-stderr-detector-negative-control` | Same detector recognizes four intentionally produced synthetic binary bytes per channel; no raw data in test output/files |
| `normal-end-kills-descendants` | Target exit terminates remaining grandchild; known test PIDs terminated afterward |
| `cancel-kills-tree` | Cancellation terminates running task and child/grandchild |
| `timeout-kills-tree` | Shortened 2-second test timeout terminates the entire managed tree |
| `host-crash-kills-tree` | Abrupt runner-host termination ends target/grandchild through job-handle closure |
| `host-crash-before-resume` | Abrupt termination before resume leaves no suspended target |
| `cleanup-failure-status` | Synthetic status injection: category 9 overrides result; actual kernel termination failures were not induced |
| `start-failure` | Invalid EXE and missing target path produce fixed categories |
| `input-paste-and-masking` | Separate test clipboard: both paste paths, selection replacement, edge spaces, oversized input, locked clipboard, retry, masking, Enter prevention |
| `form-empty-cancel-and-timeout` | Empty input does not launch; Cancel and immediate injected approval timeout discard values |
| `form-explicit-once-and-clear` | Paste buttons, explicit launch, double launch attempt invokes runner once; fields/dictionary cleared |
| `form-close-cancels-running` | Window remains during cleanup and closes after confirmed cancellation |
| `ui-loop-failure-before-approval` | Actual Application.Run exception before approval: no runner launch; report only after Dispose |
| `ui-loop-failure-cleans-before-report` | Actual UI exception with live target/grandchild; both terminated before reporting |
| `ui-loop-failure-cleanup-status-priority` | Same UI failure with synthetic cleanup-status injection; category 9 survives failed error reporting |
| `form-dispose-joins-independent-cleanup` | Dispose during live tree without UI pump; context-independent cleanup confirmed, inputs cleared |
| `form-dispose-cleanup-status-priority` | Category 9 survives Dispose and subsequently processed UI continuation |
| `gui-single-instance` | Actual second GUI EXE returns category 4 without dialog |
| `gui-process-roundtrip` | Production GUI launched separately, operated only by synthetic test code; launch/result/close, exit 0, empty launcher standard channels |
| `powershell-wrapper-roundtrip` | Shared wrapper through Codex's PowerShell runtime; GUI round trip; only fixed wrapper status |
| `wrapper-busy-preserves-other-instance` | Controlled first instance stays empty, unstarted, active; second wrapper gets BUSY; old PID-only driver refuses interaction |
| `wrapper-ownership-rejects-other-and-exited-owner` | Same EXE path and creation-time ordering are insufficient without actual parent relationship; terminated owners/candidates rejected |
| `parent-options-validation` | Bound/unbound CLI, missing parameters, zero/negative/overflowed IDs/timestamps, own PID rejected |
| `parent-handle-lifetime` | Creation-time-bound handle triggers cancellation after forcibly terminating synthetic caller |
| `parent-binding-mismatch-no-start` | Mismatched creation time produces category 5 without dialog/target |
| `parent-already-ended-no-start` | Caller terminated before launcher start produces category 2 without target |
| `parent-killed-awaiting-approval` | Values entered in normal bound GUI without approval; forcible caller termination closes launcher without target |
| `parent-killed-running-tree` | Forcibly terminate only synthetic caller; GUI returns category 2; target/grandchild end within 5 seconds |
| `parent-ended-before-form-shown` | Already-set caller cancellation closes before window-handle creation without runner invocation |
| `parent-cancels-without-ui-pump` | Intentionally suspend UI message processing; target tree still terminates; subsequent UI completion/category 2 |
| `powershell-wrapper-hard-kill` | Actual wrapper with synthetic tree; nonrecursive PowerShell Kill() without finally; launcher/target/grandchild end within 5 seconds |
| `parent-watch-disposal` | Create/dispose twelve watches while caller lives; no handle growth above small tolerance |
| `repeated-launch-handle-lifetime` | Twelve more launches without handle growth beyond small tolerance |

The sandbox's PowerShell host sometimes emits a fixed `InitializeDefaultDrives` startup diagnostic. Only that exact known host message is separately tolerated in the wrapper test; arbitrary target output is not sanitized or allowed. Windows PowerShell 5.1 failed its AuthorizationManager check before script execution in this environment and is not verified here. No execution policy was changed.

The dialog was also rendered without entered values using `DrawToBitmap` and visually inspected (`artifacts/preview.png`). This checks the application's layout, not personal desktop content, and does not replace manual keyboard/clipboard interaction testing.

## Separate Codex tool cancellation check on October 27

The **test runner's** `--tool-cancel-test` mode was started from a Codex tool process with a PTY. It started the shared wrapper, normal GUI EXE, and a synthetic target with a grandchild. Automation approved only that synthetic request; the production EXE has no autofill/auto-approval test switch.

After `TOOL_CANCEL_READY`, Ctrl+C was sent through the tool channel. Previously saved numeric test PIDs were then checked: wrapper, launcher, target, and grandchild had terminated. Result: **`TOOL_CTRL_C_CLEANUP_OK`**. Metadata/results are in `artifacts/tool-cancel-state.json` and `artifacts/tool-cancel-result.txt`; neither contains secret values.

For a focused repeat, outside the regular suite:

```powershell
$previousShell = $env:SHANDOFF_TEST_POWERSHELL
try {
    $env:SHANDOFF_TEST_POWERSHELL = (Get-Process -Id $PID).Path
    .\tests\SecretHandoff.Tests\bin\Debug\net10.0-windows\SecretHandoff.Tests.exe --tool-cancel-test
    # Send Ctrl+C only after TOOL_CANCEL_READY, then check the saved test PIDs.
} finally { $env:SHANDOFF_TEST_POWERSHELL = $previousShell }
```

On October 28, monitoring of the bound caller supplemented the earlier dependency on `finally`. The wrapper passes its PID and Windows creation time; the launcher verifies and retains that exact process handle. The ten caller-monitoring groups above establish cancellation after forcible caller termination, including the actual PowerShell wrapper. Initial repeated-watch testing found additional handles from dedicated monitoring threads; the implementation now uses a released background task and passes the handle check.

**Still unverified:** the actual Codex Desktop cancel button and other host/terminal versions. The launcher detects termination of the bound process. If Codex leaves both that process and launcher alive on task cancellation, ending the chat/task alone is not detected. Manual invocations without `--watch-parent` are intentionally unbound. Forcible process termination is not complete desktop integration or security isolation.

## Relationship to MVP criteria

The 60 test groups (47 earlier plus 13 CLI groups) are not equivalent to the 16 broader acceptance criteria. A02/A06 have .NET round-trip evidence; a separate native CRT probe is missing. A03/A04/A07/A08/A09/A11/A13 have several concrete cases but not every planned variant. A10 checks standard channels using a separate host, zero-byte assertions, and a detector negative control. A12 additionally covers forcible caller termination and UI exception/Dispose with a live tree, but remains incomplete because actual desktop cancellation and full real-time 5-/15-minute limits have not been checked. A14 includes metadata snapshots and simulated prelaunch path changes; a targeted compatible/incompatible nested-job matrix is missing. A15 checks status precedence, not actual kernel failure. A16 has code review but no complete observation of file writes across all paths.

Automation identifies wrapper children through held process handles, creation time, actual parent PID, and EXE path. Window ownership is checked before mutating GUI messages. The previous PID-only `--drive-synthetic-gui` mode is disabled; the product EXE still has no automatic approval switch. Path tests use actual local files for positive cases; network drives, reparse points, and path changes are simulated per instance. Actual junction/SUBST integration tests and concurrent file manipulation have not run. Output tests do not capture other target output channels and do not establish isolation.

Next concrete checks:

1. Manually run the demo with real Windows paste paths, Escape, window closing, long metadata, and multiple DPI settings, using public synthetic values only.
2. Test actual Codex Desktop cancellation with a synthetic tree. If termination is unreliable, extend the caller contract technically before claiming that launch path is supported.
3. Complete outstanding acceptance variants, then rebuild self-contained packages and verify runtime maintenance and clean-system behavior.

Private projects, password managers, real tokens, and account access are not needed for these steps.
