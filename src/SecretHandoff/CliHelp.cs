namespace SecretHandoff;

internal static class CliHelp
{
    internal const string Text = """
        Secret Handoff – local approval for trusted programs
        Output contract: passthrough

        Usage:
          SecretHandoff.exe --secret NAME [--description TEXT] [--secret NAME ...]
            [--working-directory DIRECTORY] -- ABSOLUTE_PROGRAM.exe [ARGUMENTS...]
          SecretHandoff.exe --request ABSOLUTE_JSON_PATH
          SecretHandoff.exe --help
          SecretHandoff.exe

        Values are entered only in the local masked dialog. Arguments contain metadata only.
        --secret: 1–16 names; optional description immediately follows its name.
        --working-directory: existing directory, default captured caller directory.
        --: everything afterward belongs to the target, including launcher-like options.
        --request: optional metadata-only JSON, cannot be mixed with direct mode.
        No arguments opens a request picker. --help must be used alone.
        Names use ASCII letters/digits/underscores, beginning with a letter or underscore,
        max 128 characters; no duplicate names or reserved runtime controls.
        Values: nonempty single-line text, max 16,384 UTF-16 code units, preserved exactly.
        At most 128 arguments; Windows encoded command-line limit applies.
        Explicit executable path; no implicit shell evaluation or PATH lookup.
        Filesystem links and mapped paths are supported. Preview is not a code identity check.

        After approval the trusted target receives ordinary runtime configuration and the
        entered secrets. Arbitrary ambient credentials are not inherited.
        Target stdout and stderr pass unchanged to the caller, including intended credentials
        or data results. The launcher does not filter output or emit the input secrets itself.
        Stdin is closed/NUL. Use an explicit interpreter EXE for an SDK-based script.
        No request file, module, or provider integration is needed for direct agent use.

        Wait for the direct target; return automatically without result dismissal.
        No fixed runtime limit, single-request lock, process-tree supervision or caller watch.
        Local Stop/closing the running window stops only the direct target. Descendants
        manage their own lifetime. Agent Stop or launcher exit may leave work running.
        The retired --watch-caller and --watch-parent options are rejected.
        Desktop launches without output handles have no output recipient.
        Returned results may be sensitive; the user approves their delivery to the caller.

        Launcher exit codes (target exit code is not passed through):
          0 COMPLETED        Target exited 0; also standalone help
          2 CANCELLED        Local cancellation
          5 INVALID_REQUEST  Invalid metadata, path or name
          6 START_FAILED     Target could not start
          7 TARGET_FAILED    Target exited nonzero; its output is still returned
          8 INTERNAL_ERROR   Launcher failure
          9 STOP_FAILED      Direct target termination could not be confirmed
        No completion banner is added to target stdout/stderr.
        PowerShell: read $LASTEXITCODE. cmd: read %ERRORLEVEL% on the next line.
        """;
}
