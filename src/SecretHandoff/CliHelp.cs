namespace SecretHandoff;

internal static class CliHelp
{
    internal const string Text = """
        Secret Handoff – local manual secret approval (synthetic prototype)

        Usage:
          SecretHandoff.exe
          SecretHandoff.exe --help
          SecretHandoff.exe --request ABSOLUTE_JSON_PATH [--watch-parent PID FILETIME]
          SecretHandoff.exe --secret NAME [--description TEXT] [--secret NAME ...]
            [--working-directory DIRECTORY] [--watch-parent PID FILETIME] -- ABSOLUTE_PROGRAM.exe [ARGUMENTS...]

        Options (exact spelling; no short forms or --option=value):
          --help                 Standalone only: help on stdout, exit 0, no dialog.
          --request PATH         Existing JSON contract; absolute local file path.
          --secret NAME          Direct mode, 1–16 required names, never values.
          --description TEXT     Optional immediately after --secret NAME, once per secret.
                                 Default: empty; at most 512 characters, no control characters.
          --working-directory D  Direct mode only, once; default: caller's working directory
                                 captured at invocation. Relative directories resolve against
                                 that snapshot. Must be an existing local directory.
          --watch-parent P T     Optional in either mode, once: positive PID (not this process)
                                 and Windows creation time as decimal UTC FILETIME.
                                 Termination of that exact verified process cancels the task.
          --                     Required in direct mode. EVERYTHING after it belongs to the
                                 target, including --help, --secret, or further -- separators.

        No options: local file picker. JSON and direct modes cannot be combined.
        --request, --working-directory, and --watch-parent may each appear only once.
        Other option ordering is free; descriptions immediately follow their secret.
        Option values must not start with --. Empty descriptions are allowed.
        Program: existing absolute local EXE path; no PATH lookup or shell evaluation.
        Arguments: at most 128 individual strings, including empty strings; quote them using
        your shell's rules. The launcher does not expand variables. Names: ASCII letter or
        underscore, followed by letters/digits/underscores, at most 128 characters;
        no case-insensitive duplicates or reserved environment/runtime names (ARCHITECTURE.md).
        No network/device/SUBST paths, reparse points, or drive-relative directories.

        Examples (synthetic names; enter values only in the local masked dialog):
          SecretHandoff.exe --secret API_TOKEN -- "C:\Tools\Worker.exe" --mode check
          SecretHandoff.exe --secret API_TOKEN --description "API for this test task" --secret SECOND_SECRET --working-directory "D:\Projects\Example" -- "C:\Tools\Worker.exe" --help
          SecretHandoff.exe --secret API_TOKEN --working-directory ".\Subfolder" -- "C:\Tools\Worker.exe"
          SecretHandoff.exe --request "D:\Projects\Example\task.handoff.json"

        Before launch: complete preview and explicit approval. No value parameters,
        automatic approval, or input reuse. No temporary JSON file in direct mode.
        Approval: 5 minutes; execution: 15 minutes; cleanup budget: 5 seconds.
        Target output is discarded. Minimal target environment; descendants can inherit values.
        An approved program can disclose values; Job Objects are not a sandbox.

        Terminal: invoke the EXE directly in PowerShell (& .\SecretHandoff.exe ...) or cmd.
        Both wait until the result window is dismissed. Read the exit code afterward:
        PowerShell: $LASTEXITCODE; cmd: %ERRORLEVEL% (on the next separate line).
        Do not use start/Start-Process without waiting. GUI use needs no prepared terminal.
        On Windows 11 24H2 and later, the manifest avoids allocating a new console;
        older Windows versions may also open a console during desktop launch.
        Direct invocation is unbound without --watch-parent. Agent cancellation is detected
        only if the launcher or bound process ends; merely stopping a wait is insufficient.
        scripts/Invoke-Handoff.ps1 binds its own PowerShell process in JSON mode.

        Exit codes (target code appears only in the local dialog; it is not passed through):
          0 COMPLETED        Target exit 0 and confirmed cleanup; also successful help
          2 CANCELLED        User cancellation or caller termination
          3 TIMED_OUT        Approval or execution timeout
          4 BUSY             Another request is active
          5 INVALID_REQUEST  Invalid CLI, contract, path, or secret name
          6 START_FAILED     Managed launch or caller monitoring failed
          7 TARGET_FAILED    Nonzero target exit with confirmed cleanup
          8 INTERNAL_ERROR   Internal failure
          9 CLEANUP_FAILED   Cleanup not confirmed (takes precedence)
        CLI errors: short fixed stderr diagnosis, no approval dialog, no raw input.
        """;
}
