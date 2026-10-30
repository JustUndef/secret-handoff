# Setup and fresh-chat test

Install the Windows application separately using the repository's [release guide](https://github.com/JustUndef/secret-handoff/blob/main/RELEASING.md). A reviewed portable build is also supported. Help must contain `Output contract: passthrough`; an old installation will otherwise discard results.

Install this complete skill folder using Codex's skill installer from `skills/secret-handoff` at a reviewed repository ref, or copy the reviewed local folder into the user's skill directory. Do not overwrite unrelated skills. The built-in installer normally uses `$CODEX_HOME/skills` (default `~/.codex/skills`). Update the existing folder rather than creating duplicate copies. Remove a retired runtime module if one exists in that same known skill folder. Restart the host if discovery has not refreshed.

No plugin, runtime module, account connection, or per-project helper is required. Other harnesses need their own supported skill discovery mechanism.

## Synthetic acceptance

Use a new chat in a disposable project. Ask the agent to run a local trusted synthetic operation requiring locally entered access secrets and return its useful result. Do not name the skill when testing implicit discovery. Record missing discovery honestly.

Use a reviewed launcher with the current output contract and the complete built SyntheticProbe directory. Invoke the probe with the argument `exchange`, requesting `EXAMPLE_API_TOKEN` and `SECOND_SECRET`. Before opening the dialog, show the human these public test values:

- EXAMPLE_API_TOKEN: synthetic-demo-one
- SECOND_SECRET: synthetic-demo-two

The human enters them locally and approves. Expect stdout `synthetic-result-credential`, stderr `synthetic-diagnostic`, and exit 0, without another result-window dismissal. The input values should not appear in normal output. This fixture is provider-independent and contacts no service.

Separately test local cancellation before approval. Actual harness Stop is not a required success condition: it may leave the task running. Do not request real accounts or tokens for development acceptance.
