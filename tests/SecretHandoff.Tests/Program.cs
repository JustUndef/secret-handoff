using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;
using SecretHandoff;
using SecretHandoff.Core;

internal static partial class Program
{
    private static string root = "", probe = "", scratch = "";
    private static int failures;
    private static readonly string[] Vector = ["", "a b", "quote\"inside", "trailing\\", "\\\"", "Unicode-\u03A9", "& | > % $ ;", "%PATH%", "${NAME}"];

    [STAThread]
    private static int Main(string[] args)
    {
        if (TryRunOutputDiscardHost(args, out int outputHostExitCode)) return outputHostExitCode;
        if (args.Length == 1 && args[0] == "--lifetime-owner")
        {
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        if (args.Length == 3 && (args[0] == "--job-host" || args[0] == "--job-host-suspended"))
        {
            probe = args[1];
            var runner = new WindowsProcessRunner
            {
                CreatedForTest = args[0] == "--job-host-suspended" ? id =>
                {
                    File.WriteAllText(args[2], id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Thread.Sleep(Timeout.Infinite);
                } : null
            };
            return (int)runner.RunAsync(Request("tree", args[2], "wait"), Values(), CancellationToken.None).GetAwaiter().GetResult().Status;
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        probe = Path.Combine(root, "tests/SyntheticProbe/bin/Debug/net10.0-windows/SyntheticProbe.exe");
        scratch = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        if (args.Length == 1 && args[0] == "--cli-tests")
        {
            RegisterCliTests();
            Console.WriteLine(failures == 0 ? "ALL_CLI_TESTS_PASSED" : "CLI_TESTS_FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length == 1 && args[0] == "--tool-cancel-test")
        {
            try { HoldWrapperForCancellation(); return 0; }
            catch { Console.WriteLine("TOOL_CANCEL_SETUP_FAILED"); return 1; }
        }
        if (args.Length > 0 && args[0] == "--drive-synthetic-gui")
        {
            // A caller-supplied PID/path proves neither test ownership nor a synthetic request.
            // Keep an explicit rejection so old invocations cannot fall through to the full suite.
            Console.WriteLine("SYNTHETIC_GUI_DRIVER_DISABLED");
            return 1;
        }
        if (args.Length == 2 && args[0] == "--render-preview")
        {
            using var form = new ApprovalForm(Request("demo"), () => null);
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, form.ClientRectangle with { Width = form.Width, Height = form.Height });
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            return 0;
        }
        Test("request-contract", RequestContract);
        RegisterCliTests();
        RegisterLocalPathTests();
        Test("environment-and-arguments", () => AssertResult(Run(Request(["check", .. Vector])), Status.Completed));
        Test("pre-cancel-no-start", PreCancel);
        Test("cancel-during-suspended-start", CancelStarting);
        Test("invalid-values-no-start", InvalidValues);
        Test("target-failure", () => { var r = Run(Request("fail")); AssertResult(r, Status.TargetFailed); Assert(r.TargetExitCode == 37); });
        Test("stdout-stderr-flood-discarded", OutputFloodDiscarded);
        Test("stdout-stderr-detector-negative-control", OutputDetectorNegativeControl);
        Test("normal-end-kills-descendants", NormalDescendants);
        Test("cancel-kills-tree", CancelTree);
        Test("timeout-kills-tree", TimeoutTree);
        Test("host-crash-kills-tree", CrashTree);
        Test("host-crash-before-resume", CrashSuspended);
        Test("cleanup-failure-status", () =>
        {
            var runner = new WindowsProcessRunner { FailCleanupForTest = true };
            AssertResult(runner.RunAsync(Request("fail"), Values(), CancellationToken.None).GetAwaiter().GetResult(), Status.CleanupFailed);
        });
        Test("start-failure", StartFailure);
        Test("input-paste-and-masking", InputPaste);
        Test("form-empty-cancel-and-timeout", FormCancellation);
        Test("form-explicit-once-and-clear", FormStart);
        Test("form-close-cancels-running", FormCloseRunning);
        RegisterUiFailureTests();
        Test("gui-single-instance", SingleInstance);
        Test("gui-process-roundtrip", ExternalGui);
        Test("powershell-wrapper-roundtrip", WrapperGui);
        Test("wrapper-busy-preserves-other-instance", WrapperBusyPreservesOtherInstance);
        Test("wrapper-ownership-rejects-other-and-exited-owner", WrapperOwnershipRejectsOtherAndExitedOwner);
        Test("parent-options-validation", ParentOptions);
        Test("parent-handle-lifetime", ParentHandleLifetime);
        Test("parent-binding-mismatch-no-start", () => ParentEarlyExit(mismatch: true));
        Test("parent-already-ended-no-start", () => ParentEarlyExit(mismatch: false));
        Test("parent-killed-awaiting-approval", () => ParentKilled(runTarget: false));
        Test("parent-killed-running-tree", () => ParentKilled(runTarget: true));
        Test("parent-ended-before-form-shown", ParentBeforeShown);
        Test("parent-cancels-without-ui-pump", ParentWithoutUiPump);
        Test("powershell-wrapper-hard-kill", WrapperHardKill);
        Test("parent-watch-disposal", ParentWatchDisposal);
        Test("repeated-launch-handle-lifetime", Repeated);
        Console.WriteLine(failures == 0 ? "ALL_TESTS_PASSED" : "TESTS_FAILED");
        // Keep only value-free summary files. No recursive deletion of external paths.
        return failures == 0 ? 0 : 1;
    }

    private static void Test(string name, Action body)
    {
        try { body(); Console.WriteLine("PASS " + name); }
        catch (TestFailure e) { failures++; Console.WriteLine("FAIL " + name + " " + e.Message); }
        catch { failures++; Console.WriteLine("FAIL " + name + " UNEXPECTED"); }
    }

    private sealed class TestFailure(string code) : Exception(code);
    private static void Assert(bool ok, string code = "ASSERT") { if (!ok) throw new TestFailure(code); }
    private static void AssertResult(RunResult result, Status expected) => Assert(result.Status == expected,
        $"STATUS_{(int)result.Status}_EXPECTED_{(int)expected}_WIN32_{result.WindowsError ?? 0}_TARGET_{result.TargetExitCode ?? 0}");
    private static LaunchRequest Request(params string[] args) => new("synthetic-test", probe,
        args.ToImmutableArray(), Path.GetDirectoryName(probe)!,
        [new("EXAMPLE_API_TOKEN", "Synthetic test value"), new("SECOND_SECRET", "Second test value")]);
    private static Dictionary<string, string> Values() => new(StringComparer.Ordinal)
    { ["EXAMPLE_API_TOKEN"] = " synthetic-only-\u03A9\\\" ", ["SECOND_SECRET"] = "synthetic-two" };
    private static RunResult Run(LaunchRequest request) => new WindowsProcessRunner().RunAsync(request, Values(), CancellationToken.None).GetAwaiter().GetResult();

    private static string WriteRequest(string json)
    {
        string path = Path.Combine(scratch, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Json() => JsonSerializer.Serialize(new
    {
        executable = probe, arguments = new[] { "fail" }, workingDirectory = Path.GetDirectoryName(probe),
        secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "Synthetic test value" } }
    });

    private static void RequestContract()
    {
        string path = WriteRequest(Json());
        var snapshot = RequestLoader.Load(path);
        Assert(snapshot.Arguments[0] == "fail");
        File.WriteAllText(path, "{}");
        Assert(snapshot.Arguments[0] == "fail");
        string json = Json();
        foreach (var bad in new[] { "{}", "not-json", json.Replace("\"arguments\":", "\"extra\":0,\"arguments\":"),
            json.Replace("\"arguments\":", "\"arguments\":[],\"arguments\":"),
            json.Replace("EXAMPLE_API_TOKEN", "Path"), json.Replace("EXAMPLE_API_TOKEN", "NODE_OPTIONS"),
            json.Replace("EXAMPLE_API_TOKEN", "NAME\u202e"), new string(' ', 65537) })
        {
            bool rejected = false;
            try { RequestLoader.Load(WriteRequest(bad)); } catch (RequestException) { rejected = true; }
            Assert(rejected, "REQUEST_ACCEPTED");
        }
        Assert(!EnvironmentPolicy.AllowedName("NAME\n"));
        Assert(!EnvironmentPolicy.ValidValue("")); Assert(!EnvironmentPolicy.ValidValue("x\0y"));
        Assert(!EnvironmentPolicy.ValidValue("x\ny")); Assert(!EnvironmentPolicy.ValidValue(new string('X', 16385)));
        Assert(EnvironmentPolicy.ValidValue(" x "));
    }

    private static void PreCancel()
    {
        string marker = Path.Combine(scratch, "pre-cancel");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var values = Values();
        AssertResult(new WindowsProcessRunner().RunAsync(Request("tree", marker, "wait"), values, cancel.Token).GetAwaiter().GetResult(), Status.Cancelled);
        Assert(!File.Exists(marker) && values.Count == 0);
    }

    private static void InvalidValues()
    {
        foreach (string value in new[] { "", "a\nb", "a\0b", new string('X', 16385) })
        {
            var values = Values(); values["SECOND_SECRET"] = value;
            AssertResult(new WindowsProcessRunner().RunAsync(Request("fail"), values, CancellationToken.None).GetAwaiter().GetResult(), Status.InvalidRequest);
            Assert(values.Count == 0);
        }
    }

    private static void CancelStarting()
    {
        string marker = Path.Combine(scratch, "cancel-starting");
        using var cancel = new CancellationTokenSource();
        var runner = new WindowsProcessRunner { CreatedForTest = _ => cancel.Cancel() };
        AssertResult(runner.RunAsync(Request("tree", marker, "wait"), Values(), cancel.Token).GetAwaiter().GetResult(), Status.Cancelled);
        Assert(!File.Exists(marker) && !File.Exists(marker + ".child"));
    }

    private static void NormalDescendants()
    {
        string marker = Path.Combine(scratch, "normal");
        AssertResult(Run(Request("tree", marker, "exit")), Status.Completed);
        AssertTreeDead(marker);
    }

    private static void CancelTree()
    {
        string marker = Path.Combine(scratch, "cancel");
        using var cancel = new CancellationTokenSource();
        var task = new WindowsProcessRunner().RunAsync(Request("tree", marker, "wait"), Values(), cancel.Token);
        try { WaitMarker(marker); cancel.Cancel(); AssertResult(task.GetAwaiter().GetResult(), Status.Cancelled); AssertTreeDead(marker); }
        finally { cancel.Cancel(); task.GetAwaiter().GetResult(); }
    }

    private static void TimeoutTree()
    {
        string marker = Path.Combine(scratch, "timeout");
        AssertResult(new WindowsProcessRunner(TimeSpan.FromSeconds(2)).RunAsync(Request("tree", marker, "wait"), Values(), CancellationToken.None).GetAwaiter().GetResult(), Status.TimedOut);
        AssertTreeDead(marker);
    }

    private static void CrashTree()
    {
        string marker = Path.Combine(scratch, "crash");
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--job-host"); info.ArgumentList.Add(probe); info.ArgumentList.Add(marker);
        using var host = Process.Start(info)!;
        try { WaitMarker(marker); host.Kill(); Assert(host.WaitForExit(5000)); AssertTreeDead(marker); }
        finally { if (!host.HasExited) { host.Kill(); host.WaitForExit(); } }
    }

    private static void WaitMarker(string marker)
    {
        var watch = Stopwatch.StartNew();
        while (!File.Exists(marker) && watch.ElapsedMilliseconds < 8000) Thread.Sleep(10);
        Assert(File.Exists(marker), "NO_START_MARKER");
    }

    private static void CrashSuspended()
    {
        string marker = Path.Combine(scratch, "crash-suspended");
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--job-host-suspended"); info.ArgumentList.Add(probe); info.ArgumentList.Add(marker);
        using var host = Process.Start(info)!;
        try
        {
            WaitMarker(marker); host.Kill(); Assert(host.WaitForExit(5000));
            int pid = int.Parse(File.ReadAllText(marker), System.Globalization.CultureInfo.InvariantCulture);
            try { using var process = Process.GetProcessById(pid); Assert(process.WaitForExit(5000)); }
            catch (ArgumentException) { }
            Assert(!File.Exists(marker + ".child"));
        }
        finally { if (!host.HasExited) { host.Kill(); host.WaitForExit(); } }
    }

    private static void AssertTreeDead(string marker)
    {
        WaitMarker(marker);
        foreach (string file in new[] { marker, marker + ".child" })
        {
            int pid = int.Parse(File.ReadAllText(file), System.Globalization.CultureInfo.InvariantCulture);
            try { using var p = Process.GetProcessById(pid); Assert(p.WaitForExit(5000), "PROCESS_REMAINED"); }
            catch (ArgumentException) { /* Already gone. */ }
        }
    }

    private static void StartFailure()
    {
        string fake = Path.Combine(scratch, "invalid.exe"); File.WriteAllBytes(fake, [0, 1, 2]);
        AssertResult(Run(Request("fail") with { Executable = fake }), Status.StartFailed);
        AssertResult(Run(Request("fail") with { Executable = fake + ".missing" }), Status.InvalidRequest);
    }

    private static void InputPaste()
    {
        string? clipboard = "synthetic-paste";
        bool blocked = false;
        int reads = 0;
        string error = "";
        using var input = new SecretInput(() => { reads++; if (blocked) throw new InvalidOperationException(); return clipboard; }, s => error = s);
        input.CreateControl();
        Assert(reads == 0 && input.UseSystemPasswordChar && !input.ShortcutsEnabled);
        input.Text = "left-right"; input.Select(4, 1);
        Assert(input.HandleSecretKey(Keys.Control | Keys.V));
        Assert(input.Text == "leftsynthetic-pasteright" && reads == 1);
        input.SelectAll(); clipboard = " replacement "; input.PasteValue(); Assert(input.Text == clipboard && reads == 2);
        clipboard = new string('X', 16385); input.PasteValue(); Assert(input.Text == " replacement " && error.Length > 0);
        blocked = true; input.PasteValue(); Assert(!error.Contains("synthetic-paste"));
        blocked = false; clipboard = "retry"; input.SelectAll(); input.PasteValue(); Assert(input.Text == "retry");
        Assert(input.HandleSecretKey(Keys.Enter));
        input.Clear(); Assert(input.TextLength == 0);
    }

    private static void FormCancellation()
    {
        int starts = 0;
        Task<RunResult> Fake(LaunchRequest _, Dictionary<string, string> values, CancellationToken token)
        { starts++; values.Clear(); return Task.FromResult(new RunResult(Status.Completed)); }
        using (var form = new ApprovalForm(Request("fail"), () => "synthetic", Fake))
        {
            form.Show(); Application.DoEvents();
            form.StartApprovedAsync().GetAwaiter().GetResult(); Assert(starts == 0 && !form.StartButton.Enabled);
            Assert(form.AcceptButton == null);
            form.Inputs[0].Text = "synthetic"; form.CancelOrClose(); Assert(starts == 0 && form.Result.Status == Status.Cancelled);
        }
        using (var form = new ApprovalForm(Request("fail"), () => "synthetic", Fake, TimeSpan.Zero))
        {
            form.Show(); Application.DoEvents(); form.CheckApprovalTimeout();
            Assert(form.Result.Status == Status.TimedOut && starts == 0 && form.Inputs.All(i => i.TextLength == 0));
            form.Close();
        }
    }

    private static void FormStart()
    {
        int starts = 0;
        var completion = new TaskCompletionSource<RunResult>();
        Dictionary<string, string>? received = null;
        using var form = new ApprovalForm(Request("fail"), () => "synthetic-ui",
            (_, values, _) => { starts++; received = values; return completion.Task; });
        form.Show(); Application.DoEvents();
        Assert(form.Preview.Contains(probe));
        foreach (var button in form.PasteButtons) button.PerformClick();
        Assert(form.StartButton.Enabled);
        var first = form.StartApprovedAsync(); var second = form.StartApprovedAsync();
        Assert(starts == 1 && received?.Count == 2 && form.Inputs.All(i => i.TextLength == 0));
        completion.SetResult(new RunResult(Status.Completed));
        Pump(first); Pump(second);
        Assert(received!.Count == 0 && form.Result.Status == Status.Completed && !form.StartButton.Enabled);
        form.Close();
    }

    private static void Pump(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(5); }
        Assert(task.IsCompleted, "UI_WAIT"); task.GetAwaiter().GetResult();
    }

    private static void Repeated()
    {
        AssertResult(Run(Request("fail")), Status.TargetFailed);
        using var self = Process.GetCurrentProcess(); self.Refresh(); int before = self.HandleCount;
        for (int i = 0; i < 12; i++) AssertResult(Run(Request("fail")), Status.TargetFailed);
        self.Refresh(); Assert(self.HandleCount <= before + 4, "HANDLE_GROWTH");
    }

    private static void FormCloseRunning()
    {
        var done = new TaskCompletionSource<RunResult>();
        CancellationToken token = default;
        using var form = new ApprovalForm(Request("fail"), () => "synthetic-ui",
            (_, _, ct) => { token = ct; return done.Task; });
        form.Show(); Application.DoEvents();
        foreach (var input in form.Inputs) input.Text = "synthetic-ui";
        var task = form.StartApprovedAsync();
        form.Close(); Assert(token.IsCancellationRequested && !form.IsDisposed);
        done.SetResult(new RunResult(Status.Cancelled)); Pump(task);
        Assert(form.IsDisposed);
    }

    private static void SingleInstance()
    {
        string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        using var mutex = new Mutex(true, @"Local\SecretHandoff." + sid);
        string gui = Path.Combine(root, "src/SecretHandoff/bin/Debug/net10.0-windows/SecretHandoff.exe");
        using var process = Process.Start(new ProcessStartInfo(gui) { UseShellExecute = false, CreateNoWindow = true })!;
        try { Assert(process.WaitForExit(5000), "GUI_BUSY_WAIT"); Assert(process.ExitCode == 4); }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } mutex.ReleaseMutex(); }
    }

    private static void ExternalGui()
    {
        string path = WriteRequest(JsonSerializer.Serialize(new
        {
            executable = probe, arguments = new[] { "demo" }, workingDirectory = Path.GetDirectoryName(probe),
            secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "Synthetic test" },
                new { name = "SECOND_SECRET", description = "Synthetic test" } }
        }));
        string gui = Path.Combine(root, "src/SecretHandoff/bin/Debug/net10.0-windows/SecretHandoff.exe");
        var info = new ProcessStartInfo(gui) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--request"); info.ArgumentList.Add(path);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            var controls = FillAndStart(process);
            var watch = Stopwatch.StartNew();
            while (!controls.Any(h => Caption(h) == "Close") && watch.ElapsedMilliseconds < 8000) Thread.Sleep(20);
            Assert(controls.Any(h => Caption(h).StartsWith("COMPLETED", StringComparison.Ordinal)), "GUI_NOT_COMPLETED");
            var close = controls.Single(h => Caption(h) == "Close");
            SendOwnedGuiMessage(process, close, 0xF5, "");
            Assert(process.WaitForExit(5000) && process.ExitCode == 0, "GUI_EXIT");
            Assert(stdout.GetAwaiter().GetResult() == "" && stderr.GetAwaiter().GetResult() == "", "GUI_OUTPUT");
        }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
    }

    // Test-only automation of our own explicitly started synthetic instance. Never accesses the OS clipboard.
    private static List<IntPtr> FillAndStart(Process process, bool startTarget = true, Process? wrapperOwner = null)
    {
        var controls = WaitOwnedGuiControls(process);
        AssertWindowOwned(process, process.MainWindowHandle, wrapperOwner);
        var edits = controls.Where(h => ClassName(h).StartsWith("WindowsForms10.Edit", StringComparison.OrdinalIgnoreCase) &&
            (GetWindowLongW(h, -16) & 0x4) == 0).OrderBy(h => { GetWindowRect(h, out var rect); return rect.Top; }).ToArray();
        Assert(edits.Length == 2, $"GUI_INPUT_COUNT_{edits.Length}");
        SendOwnedGuiMessage(process, edits[0], 0xC, "synthetic-demo-one", wrapperOwner);
        SendOwnedGuiMessage(process, edits[1], 0xC, "synthetic-demo-two", wrapperOwner);
        if (startTarget)
        {
            var start = controls.Single(h => Caption(h) == "Start");
            SendOwnedGuiMessage(process, start, 0xF5, "", wrapperOwner);
        }
        return controls;
    }

    private static Process StartLifetimeOwner()
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--lifetime-owner");
        return Process.Start(info)!;
    }

    private static long Creation(Process process) => process.StartTime.ToUniversalTime().ToFileTimeUtc();

    private static void ParentOptions()
    {
        Assert(LaunchOptions.Parse([]).RequestPath == null);
        Assert(LaunchOptions.Parse(["--request", "metadata"]).ParentId == null);
        Assert(LaunchOptions.Parse(["--request", "metadata", "--watch-parent", "123", "456"]).ParentCreation == 456);
        string self = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var bad in new string[][]
        {
            ["--request"], ["--watch-parent", "1", "2"],
            ["--request", "x", "--watch-parent", "0", "2"],
            ["--request", "x", "--watch-parent", "-1", "2"],
            ["--request", "x", "--watch-parent", "4294967296", "2"],
            ["--request", "x", "--watch-parent", "123", "0"],
            ["--request", "x", "--watch-parent", "123", "-2"],
            ["--request", "x", "--watch-parent", "123", "9223372036854775808"],
            ["--request", "x", "--watch-parent", self, "2"]
        })
        {
            bool rejected = false;
            try { LaunchOptions.Parse(bad); } catch (CliException) { rejected = true; }
            Assert(rejected, "INVALID_PARENT_OPTIONS_ACCEPTED");
        }
    }

    private static void ParentHandleLifetime()
    {
        using var owner = StartLifetimeOwner();
        try
        {
            using var watch = new ParentProcessWatch((uint)owner.Id, Creation(owner));
            Assert(!watch.Terminated.IsCancellationRequested);
            owner.Kill(); Assert(owner.WaitForExit(5000));
            Assert(watch.Terminated.WaitHandle.WaitOne(1000), "PARENT_SIGNAL_MISSING");
        }
        finally { if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(); } }
    }

    private static string TreeRequest(string marker) => WriteRequest(JsonSerializer.Serialize(new
    {
        executable = probe, arguments = new[] { "tree", marker, "wait" }, workingDirectory = Path.GetDirectoryName(probe),
        secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "Synthetic caller test" },
            new { name = "SECOND_SECRET", description = "Synthetic caller test" } }
    }));

    private static Process StartSupervisedGui(Process owner, long creation, string path)
    {
        string gui = Path.Combine(root, "src/SecretHandoff/bin/Debug/net10.0-windows/SecretHandoff.exe");
        var info = new ProcessStartInfo(gui) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in new[] { "--request", path, "--watch-parent",
            owner.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), creation.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }

    private static void ParentEarlyExit(bool mismatch)
    {
        using var owner = StartLifetimeOwner();
        string marker = Path.Combine(scratch, "parent-early-" + mismatch);
        long created = Creation(owner);
        if (!mismatch) { owner.Kill(); Assert(owner.WaitForExit(5000)); }
        using var gui = StartSupervisedGui(owner, created + (mismatch ? 1 : 0), TreeRequest(marker));
        try
        {
            Assert(gui.WaitForExit(5000), "PARENT_EARLY_GUI_REMAINED");
            Assert(gui.ExitCode == (int)(mismatch ? Status.InvalidRequest : Status.Cancelled), "PARENT_EARLY_STATUS");
            Assert(!File.Exists(marker) && !File.Exists(marker + ".child"));
        }
        finally
        {
            if (!gui.HasExited) { gui.Kill(); gui.WaitForExit(); }
            if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(); }
        }
    }

    private static void ParentKilled(bool runTarget)
    {
        using var owner = StartLifetimeOwner();
        string marker = Path.Combine(scratch, "parent-killed-" + runTarget);
        using var gui = StartSupervisedGui(owner, Creation(owner), TreeRequest(marker));
        try
        {
            FillAndStart(gui, runTarget);
            if (runTarget) WaitMarker(marker);
            var watch = Stopwatch.StartNew();
            // Kill only the synthetic owner; no finally, console signal or recursive kill.
            owner.Kill(); Assert(owner.WaitForExit(5000));
            Assert(gui.WaitForExit(5000), "GUI_SURVIVED_OWNER");
            Assert(gui.ExitCode == (int)Status.Cancelled, "PARENT_CANCEL_STATUS");
            if (runTarget) AssertTreeDead(marker);
            else Assert(!File.Exists(marker) && !File.Exists(marker + ".child"));
            Assert(watch.ElapsedMilliseconds < 5000, "PARENT_CLEANUP_TOO_SLOW");
        }
        finally
        {
            if (!gui.HasExited) { gui.Kill(); gui.WaitForExit(); }
            if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(); }
        }
    }

    private static void ParentBeforeShown()
    {
        using var parent = new CancellationTokenSource(); parent.Cancel();
        int starts = 0;
        using var form = new ApprovalForm(Request("fail"), () => null,
            (_, values, _) => { starts++; values.Clear(); return Task.FromResult(new RunResult(Status.Completed)); }, parentTermination: parent.Token);
        form.Show(); Application.DoEvents();
        Assert(starts == 0 && !form.Visible && form.Result.Status == Status.Cancelled);
    }

    private static void ParentWatchDisposal()
    {
        using var owner = StartLifetimeOwner();
        try
        {
            long created = Creation(owner);
            using (var warmup = new ParentProcessWatch((uint)owner.Id, created)) { }
            using var self = Process.GetCurrentProcess(); self.Refresh(); int before = self.HandleCount;
            for (int i = 0; i < 12; i++)
            {
                using var watch = new ParentProcessWatch((uint)owner.Id, created);
                Assert(!watch.Terminated.IsCancellationRequested);
            }
            self.Refresh(); Assert(self.HandleCount <= before + 4, "PARENT_HANDLE_GROWTH");
        }
        finally { owner.Kill(); owner.WaitForExit(); }
    }

    private static void ParentWithoutUiPump()
    {
        string marker = Path.Combine(scratch, "parent-no-ui-pump");
        using var parent = new CancellationTokenSource();
        using var form = new ApprovalForm(Request("tree", marker, "wait"), () => null, parentTermination: parent.Token);
        form.Show(); Application.DoEvents();
        foreach (var input in form.Inputs) input.Text = "synthetic-no-ui-pump";
        var task = form.StartApprovedAsync();
        try
        {
            WaitMarker(marker); // Deliberately no UI message pumping until the native tree is gone.
            parent.Cancel();
            AssertTreeDead(marker);
            Pump(task);
            Assert(form.IsDisposed && form.Result.Status == Status.Cancelled);
        }
        finally { parent.Cancel(); Pump(task); if (!form.IsDisposed) form.Close(); }
    }

    private static void WrapperHardKill()
    {
        string marker = Path.Combine(scratch, "wrapper-hard-kill");
        string shell = Environment.GetEnvironmentVariable("SHANDOFF_TEST_POWERSHELL") ?? throw new TestFailure("SHELL_REQUIRED");
        var info = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "scripts/Invoke-Handoff.ps1"), "-RequestPath", TreeRequest(marker) })
            info.ArgumentList.Add(arg);
        using var wrapper = Process.Start(info)!;
        var stdout = wrapper.StandardOutput.ReadToEndAsync(); var stderr = wrapper.StandardError.ReadToEndAsync();
        Process? gui = null;
        string phase = "FIND";
        try
        {
            var watch = Stopwatch.StartNew();
            gui = WaitForOwnedWrapperGui(wrapper);
            Assert(gui != null, "WRAPPER_HARD_GUI_MISSING");
            phase = "START_TARGET";
            FillAndStart(gui!, wrapperOwner: wrapper); WaitMarker(marker);
            watch.Restart();
            phase = "KILL_OWNER";
            wrapper.Kill(); Assert(wrapper.WaitForExit(5000)); // Nonrecursive: no PowerShell finally can execute.
            phase = "WAIT_GUI";
            Assert(gui!.WaitForExit(5000), "GUI_SURVIVED_WRAPPER_KILL");
            Assert(gui.ExitCode == (int)Status.Cancelled);
            AssertTreeDead(marker);
            Assert(watch.ElapsedMilliseconds < 5000, "WRAPPER_HARD_CLEANUP_TOO_SLOW");
            phase = "DRAIN_HOST";
            stdout.GetAwaiter().GetResult(); stderr.GetAwaiter().GetResult(); // Host diagnostics remain test-local.
        }
        catch (Exception e) when (e is not TestFailure)
        {
            string category = e is Win32Exception native ? "NATIVE_" + native.NativeErrorCode :
                e is InvalidOperationException ? "INVALID_OPERATION" : e is IOException ? "IO" : "UNEXPECTED";
            throw new TestFailure("WRAPPER_HARD_" + phase + "_" + category);
        }
        finally
        {
            if (gui != null) { if (!gui.HasExited) { gui.Kill(); gui.WaitForExit(); } gui.Dispose(); }
            if (!wrapper.HasExited) { wrapper.Kill(); wrapper.WaitForExit(); }
        }
    }

    private static List<IntPtr> Children(IntPtr window)
    {
        var children = new List<IntPtr>();
        EnumChildWindows(window, (handle, _) => { children.Add(handle); return true; }, IntPtr.Zero);
        return children;
    }

    private static void WrapperGui()
    {
        string path = Path.Combine(root, "artifacts/demo/synthetic.handoff.json");
        // Packaging must already exist for this optional integration test.
        if (!File.Exists(path)) throw new TestFailure("BUILD_DEMO_FIRST");
        string powershell = Environment.GetEnvironmentVariable("SHANDOFF_TEST_POWERSHELL") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/powershell.exe");
        var info = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "scripts/Invoke-Handoff.ps1"), "-RequestPath", path }) info.ArgumentList.Add(arg);
        using var wrapper = Process.Start(info)!;
        var output = wrapper.StandardOutput.ReadToEndAsync(); var error = wrapper.StandardError.ReadToEndAsync();
        Process? gui = null;
        try
        {
            var watch = Stopwatch.StartNew();
            gui = WaitForOwnedWrapperGui(wrapper);
            Assert(gui != null, "WRAPPER_GUI_MISSING");
            var controls = FillAndStart(gui!, wrapperOwner: wrapper);
            watch.Restart();
            while (!controls.Any(h => Caption(h) == "Close") && watch.ElapsedMilliseconds < 8000) Thread.Sleep(20);
            Assert(controls.Any(h => Caption(h).StartsWith("COMPLETED", StringComparison.Ordinal)), "WRAPPER_GUI_NOT_COMPLETED");
            var close = controls.Single(h => Caption(h) == "Close");
            SendOwnedGuiMessage(gui!, close, 0xF5, "", wrapper);
            Assert(wrapper.WaitForExit(5000) && wrapper.ExitCode == 0, "WRAPPER_EXIT");
            const string hostDiagnostic = "Attempting to perform the InitializeDefaultDrives operation on the 'FileSystem' provider failed.";
            // This Codex sandbox emits this fixed shell-startup diagnostic before the script runs.
            // Do not normalize or tolerate any other output; target output is tested independently.
            Assert(output.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim() == "HANDOFF_EXIT_0" &&
                error.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim() == "", "WRAPPER_OUTPUT");
        }
        finally
        {
            if (gui != null) { if (!gui.HasExited) { gui.Kill(); gui.WaitForExit(); } gui.Dispose(); }
            if (!wrapper.HasExited) { wrapper.Kill(); wrapper.WaitForExit(); }
        }
    }

    private static void HoldWrapperForCancellation()
    {
        string marker = Path.Combine(scratch, "tool-cancel");
        string request = WriteRequest(JsonSerializer.Serialize(new
        {
            executable = probe, arguments = new[] { "tree", marker, "wait" }, workingDirectory = Path.GetDirectoryName(probe),
            secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "Synthetic cancellation test" },
                new { name = "SECOND_SECRET", description = "Synthetic cancellation test" } }
        }));
        string shell = Environment.GetEnvironmentVariable("SHANDOFF_TEST_POWERSHELL") ?? throw new TestFailure("SHELL_REQUIRED");
        var info = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "scripts/Invoke-Handoff.ps1"), "-RequestPath", request }) info.ArgumentList.Add(arg);
        using var wrapper = Process.Start(info)!;
        Process? gui = null;
        try
        {
            gui = WaitForOwnedWrapperGui(wrapper);
            Assert(gui != null, "GUI_REQUIRED");
            FillAndStart(gui!, wrapperOwner: wrapper);
            WaitMarker(marker);
            File.WriteAllText(Path.Combine(root, "artifacts/tool-cancel-state.json"), JsonSerializer.Serialize(new
            { wrapperPid = wrapper.Id, launcherPid = gui!.Id, marker }));
            Console.WriteLine("TOOL_CANCEL_READY");
            wrapper.WaitForExit();
        }
        finally
        {
            if (gui != null) { if (!gui.HasExited) { gui.Kill(); gui.WaitForExit(); } gui.Dispose(); }
            if (!wrapper.HasExited) { wrapper.Kill(); wrapper.WaitForExit(); }
        }
    }
    private static string ClassName(IntPtr window) { var text = new StringBuilder(256); GetClassNameW(window, text, text.Capacity); return text.ToString(); }
    private static string Caption(IntPtr window) { var text = new StringBuilder(2048); GetWindowTextW(window, text, text.Capacity); return text.ToString(); }
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct WindowRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out WindowRect rectangle);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message,
        IntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
}
