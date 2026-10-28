# Contributing

Secret Handoff is a Windows prototype. Contributions are welcome, including work supported by coding agents. Full MVP acceptance is still pending; see [TESTING.md](TESTING.md).

## Before you start

Read [AGENTS.md](AGENTS.md), [README.md](README.md), and [ARCHITECTURE.md](ARCHITECTURE.md). Use [GitHub issues](https://github.com/JustUndef/secret-handoff/issues) for reproducible bugs and proposed changes. Discuss changes to the request contract, security boundary, or dependencies before implementing them. Report vulnerabilities using [SECURITY.md](SECURITY.md).

All public project material uses English, including documentation, agent instructions, code comments, application/installer text, CLI help, diagnostics, and example descriptions. Update matching UI and diagnostic tests when changing visible text. Non-ASCII synthetic fixtures remain part of Unicode compatibility coverage.

## Build from a clean checkout

Prerequisites: Windows 11 x64, PowerShell 7.3 or later, and the .NET 10 SDK selected by [global.json](https://github.com/JustUndef/secret-handoff/blob/main/global.json), including Windows Desktop support. The test runner automates its own WinForms windows and needs a Windows session that permits creating them and querying local drive and process metadata. No password-manager account, service credentials, or prepared secret environment is required.

```powershell
git clone https://github.com/JustUndef/secret-handoff.git
Set-Location secret-handoff
.\scripts\Build-Prototype.ps1 -DotnetPath (Get-Command dotnet.exe).Source
```

Without `-DotnetPath`, the scripts first look for the SDK under the user profile, then for `dotnet.exe` on PATH. An explicit path selects another existing installation. The build does not install tools, modify global PATH, or change execution policy. Prototype restore has no external NuGet sources. Release packaging has separate prerequisites and downloads; see [RELEASING.md](RELEASING.md).

The script builds the solution, creates `artifacts/demo`, and runs the custom executable test suite. This project does not use `dotnet test` to execute its tests. To build without running them:

```powershell
.\scripts\Build-Prototype.ps1 -SkipTests
```

Run the full suite after relevant changes, especially changes to process creation, handle inheritance, cancellation, or cleanup. Expect fixed `PASS` categories and `ALL_TESTS_PASSED`, with exit code 0. [TESTING.md](TESTING.md) explains focused runs, manually checked cases, and limitations. A sandbox that denies the required Windows queries cannot run the complete suite; do not weaken the path policy to make it pass. Record that limitation separately from a successful test run.

## Working with agents

[AGENTS.md](AGENTS.md) is the shared entry point. Supply it to tools that do not discover it automatically. Architecture, test evidence, release instructions, source code, and test helpers are all in this repository; contributors do not need the maintainer's chat history, personal skills, or private projects.

Commit reusable project instructions, task plans, prompts, or skills if a change depends on them. Link their entry points from AGENTS.md, remove machine-specific details, and keep one authoritative copy of each rule. There are currently no required external agent skills or additional agent configuration files. Tool-specific files are useful only when they add a real integration; avoid duplicating AGENTS.md across tools.

Do not commit personal agent configuration, chat transcripts, credential files, generated logs, screenshots, absolute-path demo requests, or local build outputs. Keep generated material in the ignored `artifacts/` or `.build/` directories. Use synthetic values only, including in agent prompts, issue reports, and screenshots. A contributor remains responsible for reviewing agent-generated changes and verifying any claimed results.

## Pull requests

The active [main branch ruleset](https://github.com/JustUndef/secret-handoff/rules/26056573) restricts updates to repository administrators using an always-allowed bypass. In this personally owned repository, that permits the owner to push directly and merge contributions; other contributors submit pull requests from a fork or another branch. No write/maintain role or app has been added to the bypass list. The declarative configuration is kept in `.github/rulesets/main.json`; editing that file does not automatically change GitHub settings.

- Keep each PR focused and preserve unrelated work. Follow the existing C# and PowerShell conventions; nullable analysis and warnings as errors are enabled.
- Describe the problem, resulting behavior, and any security or compatibility impact. Include relevant synthetic test coverage and update public documentation alongside behavior changes.
- State exactly which checks ran and their results. List skipped checks and outstanding manual validation. Do not present generated assertions or planned CI runs as executed evidence.
- Include every new source, test helper, and reusable agent artifact needed to reproduce the change. Check the diff for credentials, private paths, and accidentally generated files.
- Follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Contributions use the repository's existing [MIT License](LICENSE).

The PR workflow runs the Windows build and synthetic suite with read-only repository permissions. Tag-driven release packaging remains separate. Maintainers must review changes to workflows before running them with privileged access.
