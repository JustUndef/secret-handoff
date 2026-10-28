using System.Diagnostics;
using System.Collections.Immutable;
using System.Text;
using System.Runtime.InteropServices;
using SecretHandoff;
using SecretHandoff.Core;

internal static partial class Program
{
    private static readonly string[] CliVector = ["", "a b", "quote\"inside", "trailing\\", "\\\"", "Unicode-\u03A9", "& | > % $ ;", "--help", "--secret", "--request", "--working-directory", "--watch-parent", "--"];
    private static string Launcher => Path.GetFullPath(Path.Combine(root, "src/SecretHandoff/bin/Debug/net10.0-windows/SecretHandoff.exe"));

    private static void RegisterCliTests()
    {
        Test("cli-direct-snapshot-and-descriptions", CliSnapshot);
        Test("cli-options-rejected", CliRejected);
        Test("cli-working-directories", CliDirectories);
        Test("cli-help-and-invalid-real-processes", CliHelpProcesses);
        Test("cli-help-while-busy", CliHelpWhileBusy);
        Test("cli-console-subsystem", () =>
        {
            byte[] bytes = File.ReadAllBytes(Launcher);
            int pe = BitConverter.ToInt32(bytes, 60);
            Assert(BitConverter.ToUInt16(bytes, pe + 24 + 68) == 3, "LAUNCHER_NOT_CONSOLE_SUBSYSTEM");
        });
        Test("cli-no-arguments-picker-cancel", CliPicker);
        Test("cli-direct-default-cwd-roundtrip", () => CliDirectRoundtrip(null));
        Test("cli-direct-absolute-cwd-roundtrip", () => CliDirectRoundtrip(scratch));
        Test("cli-direct-relative-cwd-roundtrip", () => CliDirectRoundtrip("."));
        Test("cli-direct-parent-killed-tree", CliParentKilled);
        Test("cli-powershell-help-errors-wait-exits", () => CliShellTests(false));
        Test("cli-cmd-help-errors-wait-exits", () => CliShellTests(true));
    }

    private static string[] DirectArgs(params string[] targetArgs) =>
        ["--secret", "EXAMPLE_API_TOKEN", "--description", "Synthetic CLI test", "--secret", "SECOND_SECRET", "--", probe, .. targetArgs];

    private static void CliSnapshot()
    {
        var files = Directory.GetFiles(scratch).ToHashSet();
        var input = DirectArgs(["cli-check", scratch, .. CliVector]);
        var options = LaunchOptions.Parse(input, scratch);
        var request = options.DirectRequest!;
        Assert(options.RequestPath == null && request.SourcePath == "(Direct invocation)");
        Assert(request.Arguments.SequenceEqual(new[] { "cli-check", scratch }.Concat(CliVector)));
        Assert(request.Secrets.SequenceEqual(new SecretSpec[] { new("EXAMPLE_API_TOKEN", "Synthetic CLI test"), new("SECOND_SECRET", "") }));
        input[^1] = "changed";
        string previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(probe)!;
            using var form = new ApprovalForm(request, () => null);
            Assert(request.WorkingDirectory == scratch && form.Preview.Contains("Working directory: " + scratch));
            Assert(request.Arguments[^1] == "--");
        }
        finally { Environment.CurrentDirectory = previous; }
        Assert(files.SetEquals(Directory.GetFiles(scratch)), "CLI_WROTE_REQUEST");
        Assert(LaunchOptions.Parse(["--secret", "API_TOKEN", "--description", "", "--", probe], scratch).DirectRequest!.Secrets[0].Description == "");
        var file = LaunchOptions.Parse(["--watch-parent", "123", "456", "--request", "metadata"]);
        Assert(file.RequestPath == "metadata" && file.ParentCreation == 456);
    }

    private static void CliRejected()
    {
        foreach (var bad in new string[][]
        {
            ["--help", "--request", "x"], ["--request", "x", "--secret", "API_TOKEN"],
            ["--request", "x", "--", probe], ["--request", "x", "--working-directory", scratch],
            ["--request", "x", "--request", "y"], ["--request", ""], ["--unknown"], ["--secret"],
            ["--secret=API_TOKEN", "--", probe], ["--secret", "API_TOKEN", probe], ["--", probe],
            ["--secret", "API_TOKEN", "--"], ["--secret", "API_TOKEN", "--", "worker.exe"],
            ["--description", "text", "--secret", "API_TOKEN", "--", probe],
            ["--secret", "API_TOKEN", "--working-directory", scratch, "--description", "text", "--", probe],
            ["--secret", "API_TOKEN", "--description", "a", "--description", "b", "--", probe],
            ["--secret", "API_TOKEN", "--secret", "api_token", "--", probe],
            ["--secret", "PATH", "--", probe], ["--secret", "NODE_OPTIONS", "--", probe],
            ["--secret", "NAME\n", "--", probe], ["--secret", "API_TOKEN", "--description", "x\n", "--", probe],
            ["--secret", "API_TOKEN", "--description", new string('x', 513), "--", probe],
            ["--working-directory", scratch, "--working-directory", scratch, .. DirectArgs()],
            ["--watch-parent", "123", "456", "--watch-parent", "123", "456", .. DirectArgs()]
        }) CliMustReject(bad);
        CliMustReject(Enumerable.Repeat(new[] { "--secret", "NAME" }, 17).SelectMany(a => a).Concat(new[] { "--", probe }).ToArray());
        CliMustReject(DirectArgs(Enumerable.Repeat("arg", 129).ToArray()));
        CliMustReject(DirectArgs("nul\0argument"));
    }

    private static void CliMustReject(string[] args)
    {
        bool rejected = false;
        try { LaunchOptions.Parse(args, scratch); }
        catch (CliException) { rejected = true; }
        catch (RequestException) { rejected = true; }
        Assert(rejected, "CLI_INVALID_ACCEPTED");
    }

    private static void CliDirectories()
    {
        string child = Path.Combine(scratch, "cwd child \u03A9"); Directory.CreateDirectory(child);
        foreach (string directory in new[] { child, "cwd child \u03A9", ".\\cwd child \u03A9", "cwd child \u03A9\\.." })
        {
            var r = LaunchOptions.Parse(["--working-directory", directory, .. DirectArgs()], scratch).DirectRequest!;
            Assert(r.WorkingDirectory == (directory.EndsWith("..", StringComparison.Ordinal) ? scratch : child));
        }
        foreach (string bad in new[] { "", "missing-child", probe, "C:relative", "\\root-relative", "x?", "child\n", "\\\\synthetic.invalid\\share" })
            CliMustReject(["--working-directory", bad, .. DirectArgs()]);
    }

    private static Process StartCli(string[] args, string? directory = null)
    {
        var info = new ProcessStartInfo(Launcher) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory ?? scratch };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }

    private static void CliHelpProcesses()
    {
        using var help = StartCli(["--help"]);
        var output = help.StandardOutput.ReadToEndAsync(); var error = help.StandardError.ReadToEndAsync();
        try
        {
            Assert(help.WaitForExit(5000) && help.ExitCode == 0, "HELP_EXIT");
            string text = output.GetAwaiter().GetResult();
            Assert(text.Contains("--working-directory") && text.Contains("--watch-parent") && text.Contains("CLEANUP_FAILED") && error.Result == "", "HELP_OUTPUT");
        }
        finally { if (!help.HasExited) { help.Kill(); help.WaitForExit(); } }
        foreach (string[] args in new string[][] { ["--unknown-synthetic-marker"], ["--request", "x", "--secret", "NAME"], ["--working-directory", "missing", .. DirectArgs()] })
        {
            using var invalid = StartCli(args);
            var stdout = invalid.StandardOutput.ReadToEndAsync(); var stderr = invalid.StandardError.ReadToEndAsync();
            try
            {
                Assert(invalid.WaitForExit(5000) && invalid.ExitCode == 5, "CLI_INVALID_EXIT");
                string diagnostic = stderr.GetAwaiter().GetResult();
                Assert(stdout.Result == "" && diagnostic.StartsWith("INVALID_REQUEST:", StringComparison.Ordinal) &&
                    !diagnostic.Contains("unknown-synthetic-marker") && !diagnostic.Contains("missing"), "CLI_RAW_DIAGNOSTIC");
            }
            finally { if (!invalid.HasExited) { invalid.Kill(); invalid.WaitForExit(); } }
        }
    }

    private static void CliHelpWhileBusy()
    {
        string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        using var mutex = new Mutex(true, @"Local\SecretHandoff." + sid);
        try { CliHelpProcesses(); }
        finally { mutex.ReleaseMutex(); }
    }

    private static void CliPicker()
    {
        using var process = StartCli([]);
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            var watch = Stopwatch.StartNew();
            IntPtr picker = IntPtr.Zero;
            while (picker == IntPtr.Zero && IsLiveTestProcess(process) && watch.ElapsedMilliseconds < 5000)
            {
                CliEnumWindows((window, _) =>
                {
                    if (OwnershipGetWindowThreadProcessId(window, out uint id) != 0 && id == process.Id &&
                        Caption(window) == "Secret Handoff – Select request") picker = window;
                    return true;
                }, IntPtr.Zero);
                if (picker == IntPtr.Zero) Thread.Sleep(20);
            }
            AssertWindowOwned(process, picker);
            SendOwnedGuiMessage(process, picker, 0x10, "");
            Assert(process.WaitForExit(5000) && process.ExitCode == 2, "PICKER_CANCEL_EXIT");
            Assert(stdout.Result == "" && stderr.Result == "");
        }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
    }

    [DllImport("user32.dll", EntryPoint = "EnumWindows")]
    private static extern bool CliEnumWindows(EnumWindowCallback callback, IntPtr parameter);

    private static void CliDirectRoundtrip(string? directory)
    {
        string[] args = DirectArgs(["cli-check", scratch, .. CliVector]);
        if (directory != null) args = ["--working-directory", directory, .. args];
        using var process = StartCli(args);
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            var controls = FillAndStart(process, startTarget: false);
            Assert(!process.WaitForExit(150), "CLI_DID_NOT_WAIT_APPROVAL");
            Assert(controls.Any(h => Caption(h).Contains("Working directory: " + scratch)), "CLI_PREVIEW_CWD");
            SendOwnedGuiMessage(process, controls.Single(h => Caption(h) == "Start"), 0xF5, "");
            CloseCliResult(process, controls, "COMPLETED");
            Assert(process.WaitForExit(5000) && process.ExitCode == 0, "CLI_ROUNDTRIP_EXIT");
            Assert(stdout.Result == "" && stderr.Result == "", "CLI_TARGET_OUTPUT");
        }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
    }

    private static void CloseCliResult(Process process, List<IntPtr> controls, string expected)
    {
        var watch = Stopwatch.StartNew();
        while (!controls.Any(h => Caption(h) == "Close") && watch.ElapsedMilliseconds < 8000) Thread.Sleep(20);
        Assert(controls.Any(h => Caption(h).StartsWith(expected, StringComparison.Ordinal)), "CLI_RESULT_CATEGORY");
        Assert(!process.WaitForExit(150), "CLI_DID_NOT_WAIT_RESULT");
        SendOwnedGuiMessage(process, controls.Single(h => Caption(h) == "Close"), 0xF5, "");
    }

    private static void CliParentKilled()
    {
        using var owner = StartLifetimeOwner();
        string marker = Path.Combine(scratch, "cli-parent-tree");
        using var launcher = StartCli(["--watch-parent", owner.Id.ToString(), Creation(owner).ToString(), .. DirectArgs("tree", marker, "wait")]);
        var stdout = launcher.StandardOutput.ReadToEndAsync(); var stderr = launcher.StandardError.ReadToEndAsync();
        try
        {
            FillAndStart(launcher); WaitMarker(marker);
            owner.Kill(); Assert(owner.WaitForExit(5000));
            Assert(launcher.WaitForExit(5000) && launcher.ExitCode == 2, "CLI_PARENT_EXIT");
            AssertTreeDead(marker);
            Assert(stdout.Result == "" && stderr.Result == "");
        }
        finally
        {
            if (!launcher.HasExited) { launcher.Kill(); launcher.WaitForExit(); }
            if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(); }
        }
    }

    private static void CliShellTests(bool cmd)
    {
        ShellRun(cmd, ["--help"], 0);
        ShellRun(cmd, ["--request", "x", "--secret", "NAME"], 5);
        ShellRun(cmd, DirectArgs("demo"), 0, "COMPLETED");
        ShellRun(cmd, DirectArgs("fail"), 7, "TARGET_FAILED");
        ShellRun(cmd, DirectArgs("demo"), 2, "CANCEL");
    }

    private static void ShellRun(bool cmd, string[] args, int expected, string? gui = null)
    {
        string script = Path.Combine(scratch, Guid.NewGuid().ToString("N") + (cmd ? ".cmd" : ".ps1"));
        string QuotePs(string s) => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";
        string text = cmd
            ? "@echo off\r\n" + WindowsArguments.Encode(Launcher, args) + "\r\nset \"handoffExit=%ERRORLEVEL%\"\r\necho SHELL_EXIT_%handoffExit%\r\nexit /b %handoffExit%\r\n"
            : "& " + QuotePs(Launcher) + " " + string.Join(" ", args.Select(QuotePs)) + "\n$handoffExit = $LASTEXITCODE\nWrite-Output ('SHELL_EXIT_' + $handoffExit)\nexit $handoffExit\n";
        File.WriteAllText(script, text, new UTF8Encoding(false));
        string shell = cmd ? Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/cmd.exe"))
            : Environment.GetEnvironmentVariable("SHANDOFF_TEST_POWERSHELL") ?? throw new TestFailure("SHELL_REQUIRED");
        var info = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = scratch,
            RedirectStandardOutput = true, RedirectStandardError = true };
        if (cmd) info.Arguments = "/d /s /c \"\"" + script + "\"\"";
        else foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", script }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        Process? launcher = null;
        try
        {
            if (gui != null)
            {
                launcher = WaitForCliChild(process);
                var controls = FillAndStart(launcher, startTarget: false);
                Assert(!process.WaitForExit(150), "SHELL_NOT_WAITING_APPROVAL");
                if (gui == "CANCEL") SendOwnedGuiMessage(launcher, controls.Single(h => Caption(h) == "Cancel"), 0xF5, "");
                else
                {
                    SendOwnedGuiMessage(launcher, controls.Single(h => Caption(h) == "Start"), 0xF5, "");
                    Assert(!process.WaitForExit(150), "SHELL_NOT_WAITING_RESULT");
                    CloseCliResult(launcher, controls, gui);
                }
            }
            Assert(process.WaitForExit(8000), "SHELL_EXIT_TIMEOUT");
            const string hostDiagnostic = "Attempting to perform the InitializeDefaultDrives operation on the 'FileSystem' provider failed.";
            string output = stdout.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim();
            string error = stderr.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim();
            Assert(process.ExitCode == expected, "SHELL_EXIT_CODE_" + process.ExitCode + "_EXPECTED_" + expected +
                "_HELP_" + output.Contains("--working-directory") + "_MARKER_" + output.Contains("SHELL_EXIT_") +
                "_RECOGNITION_" + (error.Contains("erkannt") || error.Contains("recognized") || error.Contains("geschrieben")));
            Assert(output.EndsWith("SHELL_EXIT_" + expected, StringComparison.Ordinal), "SHELL_EXIT_OUTPUT");
            if (gui == null && expected == 0) Assert(output.Contains("--working-directory"), "SHELL_HELP_OUTPUT");
            if (expected == 5) Assert(error.StartsWith("INVALID_REQUEST:", StringComparison.Ordinal), "SHELL_ERROR_OUTPUT");
            else Assert(error == "", "SHELL_UNEXPECTED_STDERR");
            if (gui != null) Assert(output == "SHELL_EXIT_" + expected, "SHELL_TARGET_OUTPUT");
        }
        finally
        {
            if (launcher != null) { if (!launcher.HasExited) { launcher.Kill(); launcher.WaitForExit(); } launcher.Dispose(); }
            if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
        }
    }

    private static Process WaitForCliChild(Process shell)
    {
        // Reuse the held-handle, creation-time and actual-parent ownership checks.
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 6000 && IsLiveTestProcess(shell))
        {
            foreach (var candidate in Process.GetProcessesByName("SecretHandoff"))
            {
                bool accepted = false;
                try
                {
                    if (IsOwnedWrapperGui(candidate, shell, Launcher)) { accepted = true; return candidate; }
                }
                finally { if (!accepted) candidate.Dispose(); }
            }
            Thread.Sleep(20);
        }
        throw new TestFailure("CLI_SHELL_CHILD_MISSING");
    }
}
