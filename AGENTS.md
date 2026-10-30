# Working rules for Secret Handoff

## Scope and source of truth

- Read [PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md), README.md, and ARCHITECTURE.md before changes. The product contract records the owner's clarified intent. Do not turn implementation choices, test fixtures, or historical documents into new user requirements.
- Build a reusable Windows secret handoff EXE for agent-invoked trusted programs: CLI tools, SDK-based scripts, or other executables. Remain provider-independent.
- The user enters access secrets locally and approves their recipient. The program's useful stdout/stderr, including intentionally retrieved credentials, returns to the caller. Do not reintroduce blanket output suppression.
- The current execution policy waits for the direct target, returns automatically, and offers local direct-target Stop. No fixed execution timeout, Job Object supervision, caller watcher, descendant cleanup guarantee, or harness Stop acceptance requirement.
- Treat [docs/history](docs/history) as historical evidence only. Test counts from the superseded design do not establish current acceptance.
- The instruction-only [skill](skills/secret-handoff/SKILL.md) and [setup reference](skills/secret-handoff/references/setup.md) support other projects. No application-specific runtime script or request file is required.
- Public docs, comments, UI, diagnostics, examples, and instructions are English. Keep personal preferences and machine-specific configuration outside the repository.

## Secret handling and tests

- Only the human enters values in the local dialog and explicitly approves a displayed invocation. Do not automatically approve production requests or reuse previous values.
- Secret values must not be inserted into arguments, request metadata, launcher diagnostics, telemetry, shared stores, or user/system environment. Pass them only through the new target's environment; ordinary descendants may inherit them.
- The approved receiving software is trusted. Its output is intentionally returned and may be sensitive. Do not claim arbitrary output filtering, hostile-process isolation, guaranteed memory erasure, or token revocation.
- Keep executable and arguments separate; use explicit executable paths and no implicit shell evaluation. Copy only documented ordinary runtime environment variables, not arbitrary ambient credentials.
- Develop and test with synthetic values and local probes only, never real accounts, tokens, private vaults, or services. Automated test input must target only owned synthetic windows; no production autofill/approval switch.
- Test reports use fixed categories. Synthetic result/output assertions happen inside tests; do not dump environments, raw exceptions, or captured output into reports. This test-report rule is distinct from product output passthrough.
- Run scripts/Build-Prototype.ps1 for the custom suite; dotnet test does not execute it. Run scripts/Test-Skill.ps1 after skill changes. Follow CONTRIBUTING.md and SECURITY.md.
- Preserve exact input/argument behavior, output channel separation, cancellation-before-approval, direct-target Stop, concurrent independence, and launch/handle ownership with meaningful tests.

## Workspace and distribution

- Preserve unrelated changes. Do not install tools globally, publish releases, alter licensing, or change GitHub settings without authorization.
- Do not access unrelated private projects, .local files, contact data, vault entries, account configurations, or private reference history without explicit authorization.
- Include reusable instructions needed for a contribution. Exclude private configuration, chat history, logs, and build artifacts.
- Main-branch settings are separate from the declaration in .github/rulesets/main.json. Packaging and installer work do not redefine the product contract.
