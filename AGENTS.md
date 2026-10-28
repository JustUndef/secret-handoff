# Working rules for Secret Handoff

## Project scope and documentation

- Build a standalone Windows launcher for use across projects from Codex Desktop, without requiring a prepared CLI terminal.
- Read README.md and ARCHITECTURE.md before making changes. Clearly distinguish planned features, implemented behavior, and tests actually performed.
- Write all public project material in English: documentation, agent instructions, code comments, application/installer text, CLI help, diagnostics, and example descriptions. Keep personal conversation preferences and machine-specific instructions outside the repository. Preserve non-ASCII synthetic test coverage without introducing localized prose.
- Follow CONTRIBUTING.md for build prerequisites, contribution review, and agent workflows; use SECURITY.md for vulnerability reporting. Run `scripts/Build-Prototype.ps1` for the custom synthetic suite; `dotnet test` does not execute it.
- The intended main-branch update policy is recorded in `.github/rulesets/main.json` and described in CONTRIBUTING.md. GitHub settings are separate from this declaration; change them only within an explicit user request.
- Include reusable project-specific agent instructions, prompts, skills, and plans when a contribution depends on them. Link new entry points here. Do not include personal chat history, private configuration, generated logs, or local build artifacts. No external agent skills are required for this repository.
- Develop and test the prototype using synthetic values only. Real secret requests are outside the scope of development tests.
- Remain provider-independent. Do not restart general product searches or password-manager setup unless requested. Treat known supporting tools as components until their suitability for the complete workflow has been verified.

## Secrets and execution

- Use only synthetic test values. Never request real tokens in chat, retrieve account or vault contents, or contact real services for development tests.
- Accept secret values only in the local input dialog. Never put values in request files, arguments, test snapshots, logs, telemetry, or persistent storage, including an additional encrypted token store.
- Pass secrets only to the newly launched target process. Never change the environment of the launcher, caller, other running processes, or the user/system. Document inheritance by descendants and remaining memory limitations.
- Never approve automatically or reuse earlier values. Require explicit user approval of the fully displayed invocation.
- Model the program and argument list separately. Do not use implicit shell evaluation, PATH lookup, inherited credentials, or uncontrolled forwarding of target output.
- Cancellation and timeouts must terminate the managed process tree. Cover changes to process creation, handle inheritance, and cleanup with meaningful synthetic integration tests.
- Never describe output filtering or Job Objects as complete security isolation. An approved program can disclose values; application names and paths do not grant access rights.

## Workspace and changes

- Preserve existing files and unrelated changes. Do not install tools globally, publish releases, create public repositories, or change licensing without an explicit user request.
- Do not search or modify unrelated private projects, or read or import private `.local` files, contact data, vault entries, account configurations, or private repository history. Work independently when reference material is unavailable; obtain explicit user authorization before accessing private reference material.
- Restrict test and diagnostic output to fixed categories. Do not include raw exceptions, process output, or environment dumps in agent context or test reports.
- Do not promise protection against sufficiently privileged malware running as the same user, guaranteed memory erasure, or token revocation when a process exits.
