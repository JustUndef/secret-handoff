# Security policy

## Boundary

Secret Handoff accepts locally entered access secrets for a specific user-approved trusted program. The launcher does not deliberately write those secrets to storage, command arguments, its own diagnostics, chat, or the caller/user/system environment.

The approved program receives the values and is trusted to handle them. Its stdout/stderr is returned unchanged, including credentials or data intentionally requested by the user. Output is not filtered into a secrecy guarantee. Receiving programs may persist or disclose their inputs; returned results may be retained by the caller.

This application does not provide hostile-recipient isolation, protection against sufficiently privileged same-user memory inspection, guaranteed memory erasure, or token revocation. Clipboard history, OS dumps/paging and target behavior remain outside its control. Process lifetime management is not a secrecy boundary. Read [PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md) and [ARCHITECTURE.md](ARCHITECTURE.md).

## Reporting

Use [GitHub private vulnerability reporting](https://github.com/JustUndef/secret-handoff/security/advisories/new) if enabled. Its availability has not been verified here. Otherwise request a private reporting channel without posting vulnerability details or credentials publicly.

Provide version, Windows/.NET versions, impact, and a minimal local synthetic reproduction. Never provide real tokens, vault entries, environment dumps, or captured sensitive output. No response SLA or security support period is promised.

## Maintenance

Packages include a pinned .NET runtime, so runtime updates require rebuilding and redistribution. Packages are currently unsigned; checksums establish integrity against a trusted comparison, not publisher identity. Current verification and limitations are in [TESTING.md](TESTING.md).
