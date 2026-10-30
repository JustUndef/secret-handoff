> HISTORICAL RECORD: superseded on October 29, 2026. This is not current guidance or acceptance criteria. See ../../PRODUCT-CONTRACT.md and ../../TESTING.md. Links below retain their historical context.

# Open-source readiness review

Reviewed October 28, 2026. This is a repository and contributor-readiness review, not a security certification or full MVP acceptance. There is no universal checklist that certifies an open-source project as production-ready. The review uses [GitHub's community profile guidance](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) as a practical baseline.

## Scope and evidence

Reviewed the current working tree: all tracked paths, untracked project source files, shared agent guidance, public documentation, solution/build configuration, scripts, installer definitions, and GitHub workflows. The existing CLI implementation was retained; its text and matching tests were subsequently updated to English. Private files, unrelated projects, credentials, personal chat histories, and repository history were not inspected.

Read-only GitHub checks confirmed a public repository, default branch `main`, an MIT license recognized by GitHub, enabled issues, no published releases at the time of review, and successful manual packaging workflows. The [latest packaging run observed during that review](https://github.com/JustUndef/secret-handoff/actions/runs/40716000388) targets commit `792b863`; it does not validate the subsequent CLI, English-text, or readiness changes.

## Repository baseline

| Area | Review result |
| --- | --- |
| License | MIT LICENSE present and unchanged; GitHub recognizes it |
| Bundled component notices | Initial package lacked upstream runtime notices; packaging now extracts original runtime/Desktop/host license and notice files from the exact restored packages |
| Project purpose and limitations | README explains the workflow and prototype/security boundary |
| Shared agent instructions | AGENTS.md is English and included in the public source file set |
| Contributor onboarding | CONTRIBUTING.md now documents prerequisites, clean checkout, custom test runner, review expectations, and agents |
| Public documentation | Architecture, test evidence, installer evidence, and release guide translated to English |
| Application language | App-owned GUI text, CLI help/diagnostics, installer, and test descriptions now use English; matching UI tests updated |
| Community files | Code of conduct, security policy, bug/feature templates, and PR template added |
| Change history | CHANGELOG.md added with an explicitly unreleased section |
| Build portability | Scripts accept explicit tool paths and now fall back to PATH when no user-profile SDK exists |
| PR verification | Read-only Windows CI workflow added for pull requests and pushes to main; hosted results are available in GitHub Actions |
| Main branch protection | Active update restriction for refs/heads/main; only repository administrators have an always-allowed bypass |
| Action dependencies | Existing action versions resolved to official commit SHAs and pinned; weekly Dependabot configuration added |
| Release packaging | Existing installer/portable workflow retained; public agent/contributor/security guidance added to package contents |
| Package smoke check | Updated for CLI help and fixed-category invalid-request stderr; the previous zero-stderr expectation conflicted with the new CLI |
| Local artifacts | Generated outputs remain ignored; private environment/configuration files remain excluded |

## Agent artifacts and reproducibility

AGENTS.md is the only dedicated agent instruction artifact currently found in the project. No required project skills, reusable prompt library, agent plans, or other tool-specific instruction files were found. No private chat export or maintainer-only tool installation is needed to work on the project.

The shared entry points are AGENTS.md, CONTRIBUTING.md, ARCHITECTURE.md, TESTING.md, RELEASING.md, and INSTALLER-TESTING.md, backed by source-controlled build scripts and test helpers. Contributors using another agent can provide these files directly. Any future contribution depending on reusable agent prompts, skills, or plans must include those artifacts, link them from AGENTS.md, and remove personal paths/configuration first.

Source files from the CLI work, including `src/SecretHandoff/CliHelp.cs` and `tests/SecretHandoff.Tests/CliTests.cs`, are included with that implementation. Generated demo requests, synthetic result logs, screenshots, personal preferences, and chat histories are deliberately excluded from the public artifact set.

## Main branch protection follow-up

On October 28, 2026, the owner explicitly requested main protection with continued direct owner pushes. The [Protect main ruleset](https://github.com/JustUndef/secret-handoff/rules/26056573) was created and independently read back through the GitHub API: enforcement `active`, target `branch`, exactly `refs/heads/main`, rule `update`, and a single `RepositoryRole` administrator bypass (`actor_id: 5`, mode `always`). Public branch metadata now reports `protected: true`.

The configuration is recorded in `.github/rulesets/main.json`. Other contributors use pull requests; only the owner can update or merge into main in this personally owned repository. No write/maintain role or app bypass was added. Verification inspected saved settings and effective branch metadata; it did not push test commits or impersonate another account. Required CI/review rules were not added by this change.

## Remaining items

1. **Private reporting channel:** SECURITY.md describes conditional GitHub private reporting and a minimal public request for a private contact channel. Availability was not verified during the initial review. A maintainer should enable/verify a working private route before encouraging vulnerability submissions. No response SLA or security support period is promised.
2. **Hosted validation:** at the initial review, local checks had passed and hosted validation of the new workflows was outstanding. Check [Windows CI runs](https://github.com/JustUndef/secret-handoff/actions/workflows/ci.yml) and the release workflow for results against each published commit; earlier successful packaging runs cover earlier commits. The tag-driven publication path remains untested by this review; no tag/release was created. Required CI/review rules can be considered separately after hosted validation, retaining the requested owner bypass.
3. **Product acceptance:** manual clipboard/keyboard/DPI checks, the actual Codex Desktop cancel button, full timeout durations, a native CRT probe, nested-job variants, clean-machine package acceptance, and other cases in TESTING.md remain outstanding. The project is still a prototype.
4. **Distribution trust and maintenance:** packages are unsigned; checksums are not publisher authentication. Bundled runtime updates require explicit review, rebuilding, and redistribution. Do not advertise automatic runtime updates or completed release acceptance.

## Validation during this review

- Development build using the configured SDK: passed, 0 warnings and 0 errors.
- Full existing synthetic suite outside the sandbox: **60 PASS, 0 FAIL, `ALL_TESTS_PASSED`, exit 0**. Value-free summary: `artifacts/readiness-tests.txt`.
- Fresh export of the public source file set, with no existing build outputs and a synthetic user profile: build passed using the SDK found on the child process's PATH. Missing explicit SDK paths produce the expected fixed failure category.
- PowerShell parser checks for every script and `git diff --check`: passed.
- Static checks over 59 public source/configuration/documentation files: UTF-8 readable, no known credential patterns or personal workstation paths found; 15 Markdown files had balanced code fences and valid local file links. This is a limited pattern check, not a guarantee that no sensitive material can exist.
- Local self-contained `0.0.0-rc.0` ZIP/installer build: passed; no release was published. Package smoke checks cover runtime versions, required guidance/notices, help/code 0, and an invalid CLI/code 5 with bounded fixed-category stderr and no echoed metadata marker.
- All five original runtime/Desktop/host notice files matched byte-for-byte in the application directory and portable ZIP. Release-file SHA-256 checksums matched.
- Normal local install/reinstall/uninstall round trip: passed. All six isolated installer PATH groups passed with `INSTALLER_PATH_TESTS_PASSED`; test data used a separate registry area rather than the real user/system PATH. Installer tests refuse to disturb an existing installation.

After completing the English application/installer text, the development build again passed without warnings/errors and all **60 synthetic groups passed**. The packaged help, package smoke check, normal installer round trip, and all six isolated PATH groups passed again. A rendered empty-dialog preview was visually checked. Final scans of public project files and packaged Markdown found no remaining localized prose; Unicode test coverage remains enabled. Details and generated value-free artifact names are recorded in TESTING.md.

Workflows were reviewed statically, including SHA references, read-only PR permissions, and separation of publication. Local results do not replace hosted checks; current results are recorded in GitHub Actions. Earlier test counts in TESTING.md remain attributed to their original runs.

## Maintainer handoff

The public source file set includes shared agent/contributor files, the ruleset declaration, and required CLI source/test files. Verify hosted CI results and the private reporting channel separately. The owner-requested main update restriction is active on GitHub. Publishing source commits is separate from tag-driven release publication; this review installed no tools globally and made no license change.
