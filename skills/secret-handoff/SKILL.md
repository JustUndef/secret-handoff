---
name: secret-handoff
description: Use Secret Handoff to supply locally entered secrets to a user-approved CLI or SDK-based operation and return its useful output to the agent without placing the input secret in chat or arguments.
---

# Secret Handoff

Invoke the installed EXE directly. This skill contains instructions only; no runtime module, generated wrapper, or request file is needed.

The human enters access secrets in the local dialog and approves the receiving program. That program is trusted. Its output returns to you and may intentionally contain credentials or data needed for the authorized task. Do not confuse the locally entered access secret with the useful result, or suppress all output.

## Invocation

1. Determine the authorized executable, separate arguments, working directory, and required secret names. A CLI or an explicit interpreter running an SDK-based script is equally supported.
2. Use the default executable at `%LOCALAPPDATA%\Programs\Secret Handoff\SecretHandoff.exe` or a user-authorized portable/custom path. Check standalone `--help` for `Output contract: passthrough`. Older builds discard output; do not use them for this workflow. Missing/old installation: follow [setup](references/setup.md).
3. Invoke with `--secret NAME` and optional descriptions, explicit working directory, and `-- EXE ARGUMENTS`. Preserve argument boundaries through the host's normal execution tool. See [invocation details](references/invocation.md).
4. Explain the operation, then let the human enter values and approve locally. Never ask for the input secret in chat, place it in arguments/files, autofill the dialog, or read an unrelated credential store.
5. Wait for completion, polling a yielded execution session as needed. The window closes automatically. Consume the returned stdout/stderr as the authorized operation's result; keep sensitive results within the authorized task and avoid unnecessary repetition in chat.

No caller PID discovery or watch option is needed. The retired watch options are rejected. No fixed runtime limit applies. Local Stop/closing the window stops only the direct program; harness Stop or launcher exit may leave work running. Descendants manage their own lifetime.

A target's nonzero exit maps to launcher code 7; useful failure output is still returned. Do not retry cancellation or a partially completed operation automatically. Missing exit status is not success.

Input values belong only in the local dialog and the approved program. The launcher creates no secret store. The approved program can emit its input; there is no arbitrary-output safety guarantee or hostile-process sandbox. Development tests use synthetic values only.
