# Security policy

## Project status and scope

Secret Handoff is a prototype; full MVP acceptance and an independent security audit are pending. There is no guaranteed security support period for a released version. Start with the current source and check [TESTING.md](TESTING.md) for verified behavior and remaining evidence.

The launcher aims to avoid accidental secret storage and disclosure in chat. It does not isolate secrets from an approved target, sufficiently privileged processes running as the same user, or administrators. Job Objects and discarded standard output are not a sandbox. Process termination does not revoke tokens or guarantee complete memory erasure. See [ARCHITECTURE.md](ARCHITECTURE.md) for the full boundary.

## Reporting a vulnerability

Use [GitHub's private vulnerability report form](https://github.com/JustUndef/secret-handoff/security/advisories/new) **if private reporting is enabled**. Its availability has not yet been verified for this repository. If the form is unavailable, open an issue asking the maintainer to provide a private reporting channel; include no vulnerability details, exploit, private metadata, or credentials in that public request. Wait for a private channel before sharing sensitive details.

In a private report, provide the affected version or commit, relevant Windows/.NET versions, a description of the impact, and a minimal reproduction using synthetic values and local programs. Do not send real tokens, vault entries, environment dumps, raw process output containing secrets, or personal data. Avoid testing against real services or unrelated processes.

Response and remediation times depend on maintainer availability; no fixed SLA is promised. Coordinate publication of vulnerability details with the maintainer.

## Maintenance responsibilities

Release packages bundle a pinned .NET runtime. Maintainers must review official runtime security updates, update `release-runtime.json`, rebuild, test, and distribute replacement packages. The launcher has no automatic updater. Current packages are unsigned; SHA-256 checksums support integrity checks against a trusted reference, not publisher authentication.
