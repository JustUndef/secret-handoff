# Invocation and results

Use the default installed absolute EXE path or a user-authorized portable path. Standalone help must contain `Output contract: passthrough`; otherwise the build implements the retired output-discarding workflow.

Example with ordinary PowerShell native invocation (paths are illustrative):

```powershell
& "$env:LOCALAPPDATA\Programs\Secret Handoff\SecretHandoff.exe" --secret API_TOKEN --description 'Access for the approved operation' --working-directory 'D:\Projects\Example' -- 'C:\Tools\Worker.exe' --mode fetch
$LASTEXITCODE
```

Prefer a process argument-list API when available. PowerShell 7.3+ modern native argument passing preserves empty/quoted arguments; legacy shell quoting can change them. The launcher cannot undo shell processing. There is no implicit shell evaluation or secret substitution.

Only names/descriptions are supplied. Values are entered locally. Everything after the first `--` is target metadata, including options that resemble launcher options. The EXE must be an explicit existing path; an SDK script uses an explicit interpreter EXE and script arguments. Stdin is NUL.

There are 1–16 nonduplicate names, at most 128 arguments, and Windows command-line length limits. Names use ASCII letters/digits/underscores, starting with a letter or underscore. Ordinary runtime configuration/control names are reserved; broad credential-name prefixes are not. Values currently support nonempty single-line Unicode, preserving spaces, up to 16,384 UTF-16 units. No NUL or newline. Filesystem links and mapped paths are supported when accessible.

The target receives a documented allowlist of ordinary runtime environment variables plus entered secrets; arbitrary ambient credentials are not inherited. The launcher waits for the direct target without a fixed execution timeout or result-dismissal step.

Stdout and stderr pass unchanged to the caller, without buffering, filtering, text conversion, or completion banners. Returned credentials/data may be sensitive by design. The trusted program must avoid printing the input secret unless that disclosure is explicitly intended. The launcher does not promise that arbitrary output is safe.

| Code | Meaning |
| --- | --- |
| 0 | COMPLETED; direct target exited 0. Standalone help also uses 0. |
| 2 | CANCELLED locally; no target started or direct target stopped. |
| 5 | INVALID_REQUEST; review metadata and names. |
| 6 | START_FAILED. |
| 7 | TARGET_FAILED; target exited nonzero, its stdout/stderr still returned. |
| 8 | INTERNAL_ERROR. |
| 9 | STOP_FAILED; direct target termination not confirmed. |

The target numeric exit code is mapped to 0/7. No code guarantees descendant termination. An inherited output handle may keep the caller's pipe open if a descendant retains it.

Local Stop is separate from harness Stop. If a tool wait is interrupted, do not assume the operation ended; check the owned execution session or ask the user to stop the direct program locally. Do not guess process identities or automatically retry an operation that may have taken effect.
