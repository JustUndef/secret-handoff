# Changelog

## Unreleased

- October 30, 2026: silent installer roundtrip, isolated PATH tests, and old-to-new update and uninstall passed.

- October 30, 2026: recorded successful human-assisted synthetic handoff and preapproval cancellation with the repaired portable build; masking and recipient visibility confirmed. Installer/update acceptance remains open.

- Scope reset: PRODUCT-CONTRACT.md records provider-independent local secret entry for trusted programs and useful output returned to the agent.
- Target stdout/stderr now pass through unchanged, including intentional credential/data results. Completion closes automatically; no fixed execution timeout or single-request mutex.
- Removed caller-watch options, Job Object tree supervision and automatic descendant termination. Local Stop affects only the direct target.
- Ordinary runtime environment allowlist, exact reserved names instead of broad prefix bans, and filesystem-link/mapped-path compatibility.
- Updated skill, CLI help, synthetic acceptance suite, packaging and project instructions. Earlier lifecycle/output decisions below are historical.

### Earlier prototype history (superseded where noted above)

- Description-only `secret-handoff` skill and direct EXE agent workflow without a runtime module or request file; `--watch-caller` discovers and verifies only the immediate Windows parent, retaining explicit binding and manual/unbound modes.

- Windows launcher prototype with explicit local approval, masked secret input, minimal target environments, and managed process-tree cleanup.
- JSON requests, direct CLI metadata, standalone help, working-directory selection, and optional caller lifetime binding.
- Synthetic local integration tests and documented outstanding MVP acceptance criteria.
- Per-user installer, portable self-contained packaging, optional user PATH integration, and tag-driven release automation.
- English project documentation, shared agent instructions, contributor/security guidance, issue/PR templates, and a PR build/test workflow.
- English application UI, CLI help and diagnostics, installer, and synthetic test descriptions, with matching UI test updates.

No GitHub release had been published at the October 28, 2026 repository review. Test package versions and successful manual packaging workflows are not published releases. Maintainers should create a dated version section only when that version is actually released.
