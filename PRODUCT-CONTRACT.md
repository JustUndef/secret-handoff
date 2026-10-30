# Product contract

This document records the owner's clarified intent on October 29, 2026 and supersedes earlier lifecycle and output requirements. Implementation choices below are not additional user requirements. Change this contract explicitly before expanding product scope.

## Purpose

An agent requests locally entered secrets for a specific user-approved operation. Secret Handoff supplies them to the intended program and returns that program's useful output to the caller. The program may be a CLI, an SDK-based script invoked through an interpreter, or another executable. No provider, including 1Password, is special.

The access secret entered in the dialog is distinct from the operation's result. Results may intentionally contain credentials or other sensitive data that the agent needs. Returning those results is supported behavior.

## Required behavior

- Reusable Windows EXE, directly invocable with metadata arguments; an instruction-only skill teaches agents to use it. No per-project secret-entry script or request file is required.
- Show the executable, separate arguments, working directory, and requested secret names before approval. Only the human enters values and approves their use locally.
- Supply values only to the new approved process, without modifying the caller or user/system environment, writing a credential store, or placing values in command arguments or launcher diagnostics.
- Preserve entered values and argument boundaries. Do not silently trim, expand, or substitute secrets into command text.
- Return the trusted program's stdout and stderr to the caller. Do not inject launcher status text into successful target output. Return a documented completion/failure status automatically.
- Allow cancellation before approval without starting the target. Keep unrelated processes and requests independent.
- Test with synthetic values and a synthetic useful result different from the input secret. Never use development tests to access real accounts.

## Trust boundary

The approved receiving program is trusted. It can use and disclose its inputs; the launcher does not sandbox it, filter arbitrary output into guaranteed safety, or defend against hostile same-user memory inspection. The launcher itself does not deliberately emit or persist entered secrets. Returning useful output is not a claim that output can never contain the input secret. The user approves the operation knowing its output reaches the calling agent.

Avoid shared credential storage and routine disclosure. Clipboard history, OS memory management, target-created files, and the receiving program's own behavior are outside this application's control. Do not promise complete memory erasure or credential revocation.

## Execution policy chosen for this implementation

Wait for the direct target and let it use the caller's standard output/error handles. There is no fixed execution timeout, process-tree job, caller monitoring, automatic descendant termination, or second result-dismissal step. Local Stop/closing the running window attempts to terminate only the direct target; descendants manage their own lifetimes. Stopping the agent or killing the launcher is not a cancellation guarantee. This is a simple execution policy, not a secret-handoff requirement imposed by the user.

Use a documented allowlist of ordinary runtime environment variables plus explicitly entered secrets; do not copy arbitrary ambient credentials. Explicit executable paths and filesystem validation prevent accidental shell/path interpretation, not malicious code replacement. Ordinary junctions, symbolic links, mapped paths and fully qualified filesystem paths are supported when accessible.

JSON requests remain optional metadata compatibility. Single-line values and bounded metadata are current input capabilities, not a security argument against future multiline support. Packaging, CI and installer settings are distribution concerns and do not define the handoff workflow.

## Acceptance

The central acceptance test is provider-independent: the human locally enters a synthetic access secret; the approved test program verifies it and returns a different synthetic result; the caller receives that result and completion status without the input secret in normal output or launcher diagnostics. Also verify exact arguments/values, failure output, large and binary stdout/stderr, cancellation before approval, local direct-target Stop, independent concurrent requests, and no secret persistence by the launcher.

Earlier tests of mandatory tree cleanup, harness cancellation, discarded output, or restricted path types describe the superseded design. Preserve their history without treating them as current acceptance requirements.
