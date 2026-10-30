# Current test evidence

The scope authority is [PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md). Earlier test counts and supervision acceptance criteria are [historical](docs/history/pre-reset-TESTING.md).

## Scope reset verification — October 29, 2026

The revised custom synthetic suite has 22 groups. A full scripts/Build-Prototype.ps1 run with normal Windows permissions passed all 22 with ALL_TESTS_PASSED, exit 0, zero build warnings and zero errors. An earlier sandbox run failed only at junction fixture creation; the same fixture passed with normal permissions. No product validation was weakened for that environment.

| Coverage | Evidence |
| --- | --- |
| Request and CLI | Immutable metadata, bounded JSON/duplicate-key refusal, argument separator, retired watch-option refusal, invalid names/paths |
| Environment and values | Exact Unicode/space preservation, useful credential names allowed, ordinary runtime settings available, ambient synthetic credentials and NODE_OPTIONS excluded |
| Paths | Normalization and real junction acceptance; invalid/device-style metadata refused |
| Launch | Cancellation before creation and during suspended start; invalid executable fails; values dictionary released |
| Handles and stdin | Inheritable sentinel is not inherited by the target; stdin is NUL; repeated actual starts have bounded handle growth |
| Direct lifetime | Local Stop and form disposal stop the direct target; normal exit leaves an owned descendant alive, which the test then cleans up |
| Local input/UI | Masking, exact paste replacement, invalid paste, no Enter-to-approve, one launch per approval, cleared input references, automatic completion close |
| Actual EXE result | Owned synthetic approval window receives input; target returns a different synthetic credential on stdout and a separate diagnostic on stderr; exit 0; input token absent from normal captured output |
| Failure result | Actual EXE returns partial stdout and stderr on nonzero target exit; launcher code 7 |
| Large/binary output | Actual EXE forwards exactly 4 MiB on each channel, concurrently; bytes compared in memory without printing captured data |
| Compatibility and concurrency | Actual JSON invocation, independent concurrent approval windows, local cancel before approval without target start |

Tests inspect their own captured synthetic output and print fixed categories only. There is no production auto-approval switch. No real accounts, vaults, APIs, or secrets were accessed.

The instruction-only skill passes scripts/Test-Skill.ps1 (two groups). The generic skill-creator quick_validate.py could not run because its Python environment lacks PyYAML; no global dependency was installed. The project validator checks this skill's constrained frontmatter, policy metadata, references and file inventory.

## Reproduction

```powershell
.\scripts\Build-Prototype.ps1
.\scripts\Test-Skill.ps1
```

The custom test executable runs the suite; dotnet test does not. Fixture failures in restricted hosts must be distinguished from application failures. No full suite run is necessary for documentation-only edits.

## Manual acceptance and distribution limits

### Human-assisted acceptance — October 30, 2026

The new self-contained portable EXE was exercised through the installed instruction-only skill in Codex Desktop, using only local synthetic values. Its standalone help confirmed `Output contract: passthrough`.

| Check | Result |
| --- | --- |
| Human-approved handoff | PASS: exit 0; exact expected synthetic result on stdout and diagnostic on stderr, checked separately |
| Cancel before approval | PASS: exit 2; empty output channels and no fresh target-start marker or pending marker |
| Masked entry | PASS: explicitly confirmed by the human |
| Recipient preview | PASS: human confirmed visibility after a short repeat check pointing out `Program:` in the upper preview; repeat cancellation returned exit 2 |
| Automatic completion | Automated UI coverage passed and the human-operated invocation returned; no separate explicit human confirmation of automatic window closure was recorded |

The core handoff and cancellation workflow has therefore been exercised successfully by a human. This is not an installer acceptance or a claim of universal harness compatibility. The agent was explicitly directed to read the installed skill; implicit discovery in an otherwise unprompted fresh chat was not retested for this revision. Earlier discovery evidence used the old output-discarding build. The [synthetic procedure](skills/secret-handoff/references/setup.md) remains available for reproduction.

Actual Codex Stop previously left the running tree alive; an independent observer cleaned up afterward. That result remains historical evidence and is not a current acceptance gate. Current local Stop affects only the direct target.

A fresh self-contained win-x64 package (local test version 0.0.0-rc.1) and installer were built for this reset. Package smoke checks and SHA-256 verification passed. Eight additional integration groups against the packaged EXE passed: help/errors, useful result without the input token, failure output, large binary output, arguments/environment/CWD, preapproval cancellation, concurrent requests, and JSON compatibility. These checks use the actual packaged application with owned synthetic input windows. Older packages still discard output.

Installer roundtrip tests are blocked by the existing-installation guard on this machine. The existing installation was not overwritten and remains older than the tested portable build. The repository skill passed static validation and the previously installed four-file skill was backed up, updated and hash-verified against the reviewed source. The human-assisted checks above used this updated skill and the new portable EXE together.

Broader compatibility (other harnesses, legacy shells, native programs with custom argument parsers, other Windows versions and high-DPI/clipboard interaction) is not established by the synthetic suite. No public release is claimed. Hosted CI results are recorded separately from local acceptance evidence.

## Remaining distribution work

Hosted Windows CI passed for source commit `ebcdc53ffe3a6cacf2af90d5ffb6f1719436795b`: build/custom synthetic suite and instruction-only skill validation both succeeded. See [the October 30, 2026 run](https://github.com/JustUndef/secret-handoff/actions/runs/41073256881). The subsequent evidence-only documentation update does not change the tested application, tests, skill or workflows.

- Exercise clean installation, update and uninstall on a disposable Windows environment or an explicitly approved existing installation.
- Choose and publish a release version deliberately after distribution checks; local `0.0.0-rc.1` artifacts are test builds, not a published 1.0 release.
