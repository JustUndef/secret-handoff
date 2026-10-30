# Historical Desktop Stop investigation

The old independent-observer procedure is [archived](docs/history/pre-reset-DESKTOP-STOP-TEST.md). Its observer/test implementation was retired with mandatory caller and process-tree supervision.

That historical run found all observed processes alive after the Codex Stop action. The current application does not promise that harness Stop terminates work. Use the local Stop control to stop the direct target; descendants manage their own lifetime.

The current product contract is [PRODUCT-CONTRACT.md](PRODUCT-CONTRACT.md). Harness integration is not an MVP acceptance requirement unless explicitly requested as a future feature.
