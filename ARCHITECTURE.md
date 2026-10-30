# Architecture

[PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md) is the scope authority. This document describes implementation choices after the October 29, 2026 reset. Earlier supervision architecture is [historical](docs/history/pre-reset-ARCHITECTURE.md).

## Flow and trust

Agent -> SecretHandoff.exe -> trusted approved program -> stdout/stderr back to agent.

The user enters values locally. The immutable request contains only executable, arguments, working directory, and secret names/descriptions. The target is trusted with those values. It may intentionally return sensitive credentials or data. Output is not filtered; the launcher itself does not print entered secrets or write credential storage. This is not a sandbox, credential revocation mechanism, or guarantee against same-user memory inspection.

## Components

- LaunchOptions/CliHelp parse metadata and expose the direct CLI. JSON is optional compatibility.
- RequestLoader/LocalPathPolicy validate and snapshot explicit filesystem paths, argument limits, names, and display text. Ordinary filesystem links and mapped paths are allowed. Device namespaces, alternate data streams, ambiguous trailing dots/spaces, and misleading display characters are rejected. Validation does not lock executable identity.
- SecretInput/ApprovalForm provide masked input, exact paste handling, complete invocation preview, and explicit approval. No approval autofill exists in production.
- ApprovalLifecycle owns one invocation and its cancellation independently of UI callbacks. It does not monitor an external caller.
- EnvironmentPolicy builds a new environment block from documented runtime names and locally entered secrets.
- WindowsProcessRunner creates the direct process, waits for it, and stops only that process on local cancellation/error.

C#/WinForms on .NET 10 remains the Windows implementation. Console subsystem and the existing detached-console manifest permit direct shell invocation and a local GUI. Older Windows console-display behavior needs separate validation. Self-contained packages include the runtime.

## Request/input capabilities

```text
SecretHandoff.exe --secret NAME [--description TEXT] [--secret NAME ...]
  [--working-directory DIRECTORY] -- ABSOLUTE_PROGRAM.exe [ARGUMENTS...]
SecretHandoff.exe --request ABSOLUTE_JSON_PATH
SecretHandoff.exe --help
```

Descriptions immediately follow their secret. Everything after the first separator belongs to the target. Relative working directories resolve once against the captured caller directory. The target is an existing explicit .exe; scripts use an explicitly approved interpreter. No PATH search, shell expansion, or value interpolation.

Current bounds: 1–16 secrets, 128 arguments, 128 characters per name, 512 per description; single-line nonempty values up to 16,384 UTF-16 units. Preserve Unicode and leading/trailing spaces; reject NUL and line breaks. Windows command-line length limits apply. These are current capabilities, not an assertion that other secret formats are unsafe.

JSON has exactly executable, arguments, workingDirectory and secrets; each secret has name and description. Files are read once with a 64 KiB bound. Reject duplicate/unknown keys and incorrect types. No values are written into requests.

## Environment and output

Copy only these ordinary process-environment names when present: SystemRoot, WINDIR, PATH, PATHEXT, COMSPEC, TEMP, TMP, USERPROFILE, HOMEDRIVE, HOMEPATH, APPDATA, LOCALAPPDATA, PROGRAMDATA, PROGRAMFILES, PROGRAMFILES(X86), PROGRAMW6432, PROCESSOR_ARCHITECTURE and NUMBER_OF_PROCESSORS. Do not copy arbitrary credential variables or runtime-injection settings. Entered secret names may not override those names or the exact runtime controls listed in EnvironmentPolicy.cs. Broad prefix bans are removed; names such as NODE_AUTH_TOKEN and OP_SERVICE_ACCOUNT_TOKEN work. This allowlist is a compatibility/security tradeoff, not a sandbox.

The target's environment is constructed separately; caller/user/system variables never receive the values. The temporary native block is overwritten after creation and managed references are released early. This does not guarantee erasure of all UI, managed, OS or target copies.

The runner duplicates the caller's stdout/stderr handles and explicitly inherits only those plus NUL stdin via a process attribute handle list. There are no launcher output buffers, files, filters, or text re-encoding. Large/binary output and separate stderr survive. A desktop launch without output handles has no output recipient and uses NUL for missing channels.

A child can inherit these channels from the target. Consequently a caller waiting for pipe EOF may wait for descendants even after the direct target exits. The launcher neither kills those descendants nor claims to manage background services. Prefer an operation that completes its intended output before exiting.

## Lifetime and results

Creation is briefly suspended to finish input cleanup and recheck local cancellation before execution. There is no Job Object, caller watcher, fixed approval/execution timer, or single-request mutex. Independent requests are independent.

Wait for direct target termination and return automatically; there is no result-dismissal step. Local Stop/closing a running window requests direct-process termination and waits up to five seconds for confirmation. UI disposal joins this bounded cancellation path. It does not terminate descendants. Launcher crashes or harness Stop may leave the target running. A premature caller exit is not a secret authorization signal.

Launcher codes: 0 COMPLETED, 2 CANCELLED, 5 INVALID_REQUEST, 6 START_FAILED, 7 TARGET_FAILED, 8 INTERNAL_ERROR, 9 STOP_FAILED. Codes 3 and 4 and caller-watch options are retired. Target numeric exit is mapped to 0 or 7; output remains available on failure. Configuration errors use fixed stderr messages without raw arguments or exceptions. Successful operations add no launcher output.

## Verification

See [TESTING.md](TESTING.md). The central test enters a synthetic access secret locally and receives a different useful result. Exact arguments/values, independent output channels, binary output, cancellation, UI ownership and concurrent requests are tested. Historical process-tree cleanup and discarded-output tests are not current acceptance criteria.
