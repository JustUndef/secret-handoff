using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SecretHandoff;
using SecretHandoff.Core;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
    private static string root = "", probe = "", scratch = "", launcher = "";
    private static int failures;
    private static readonly string[] Vector = ["", "a b", "quote\"inside", "trailing\\", "\\\"", "Unicode-\u03A9", "& | > % $ ;", "%PATH%", "${NAME}", "--help", "--watch-caller", "--"];

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        probe = Path.GetFullPath(Path.Combine(root, "tests/SyntheticProbe/bin/Debug/net10.0-windows/SyntheticProbe.exe"));
        launcher = Path.GetFullPath(Path.Combine(root, "src/SecretHandoff/bin/Debug/net10.0-windows/SecretHandoff.exe"));
        scratch = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] != "--packaged-launcher" || !Path.IsPathFullyQualified(args[1]) || !File.Exists(args[1]))
            { Console.WriteLine("INVALID_TEST_MODE"); return 1; }
            launcher = Path.GetFullPath(args[1]);
            Test("packaged-help-and-invalid-diagnostics", Help);
            Test("packaged-useful-result-and-no-input-secret", () => GuiResult("exchange", 0, "synthetic-result-credential\r\n", "synthetic-diagnostic\r\n"));
            Test("packaged-failure-output", () => GuiResult("fail", 7, "partial-result", "synthetic-failure"));
            Test("packaged-large-binary-output", Flood);
            Test("packaged-argv-cwd-environment", Roundtrip);
            Test("packaged-cancel-before-approval", CancelGui);
            Test("packaged-concurrent-requests", ConcurrentGui);
            Test("packaged-json-compatibility", JsonGui);
            Console.WriteLine(failures == 0 ? "ALL_PACKAGE_INTEGRATION_TESTS_PASSED" : "PACKAGE_INTEGRATION_TESTS_FAILED");
            return failures == 0 ? 0 : 1;
        }
        Test("request-snapshot-and-invalid-json", RequestContract);
        Test("cli-argument-boundaries-and-options", CliContract);
        Test("environment-names-and-value-validation", EnvironmentContract);
        Test("path-normalization-and-junction", Paths);
        Test("help-and-fixed-invalid-diagnostics", Help);
        Test("local-input-masking-paste-exactness", Input);
        Test("pre-cancel-and-invalid-values-no-launch", BeforeStart);
        Test("cancel-during-suspended-start", CancelStarting);
        Test("start-failure-and-value-release", StartFailure);
        Test("restricted-handle-inheritance-and-null-stdin", HandleInheritance);
        Test("local-stop-direct-target", StopDirect);
        Test("normal-exit-preserves-descendant", Descendants);
        Test("form-once-clear-and-auto-close", FormOnce);
        Test("form-disposal-stops-direct-target", FormDisposal);
        Test("actual-exe-useful-result-and-no-input-secret", () => GuiResult("exchange", 0, "synthetic-result-credential\r\n", "synthetic-diagnostic\r\n"));
        Test("actual-exe-failure-output", () => GuiResult("fail", 7, "partial-result", "synthetic-failure"));
        Test("actual-exe-large-binary-output", Flood);
        Test("actual-exe-argv-cwd-environment-roundtrip", Roundtrip);
        Test("actual-exe-cancel-before-approval", CancelGui);
        Test("actual-exe-independent-concurrent-requests", ConcurrentGui);
        Test("actual-exe-json-compatibility", JsonGui);
        Test("repeated-launch-handles", Repeated);
        Console.WriteLine(failures == 0 ? "ALL_TESTS_PASSED" : "TESTS_FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Test(string name, Action run)
    {
        try { run(); Console.WriteLine("PASS " + name); }
        catch (Failure f) { failures++; Console.WriteLine("FAIL " + name + " " + f.Message); }
        catch { failures++; Console.WriteLine("FAIL " + name + " UNEXPECTED"); }
    }
    private sealed class Failure(string category) : Exception(category);
    private static void Assert(bool success, string category = "ASSERT") { if (!success) throw new Failure(category); }
    private static void Reject(Action action)
    {
        try { action(); } catch (RequestException) { return; } catch (CliException) { return; }
        throw new Failure("INVALID_ACCEPTED");
    }
    private static LaunchRequest Request(params string[] args) => RequestLoader.Create(probe, args, Path.GetDirectoryName(probe)!,
        [new("EXAMPLE_API_TOKEN", "Synthetic access input"), new("SECOND_SECRET", "Synthetic second input")]);
    private static Dictionary<string, string> Values() => new(StringComparer.Ordinal)
        { ["EXAMPLE_API_TOKEN"] = "synthetic-demo-one", ["SECOND_SECRET"] = "synthetic-demo-two" };
    private static string[] Args(params string[] target) =>
        ["--secret", "EXAMPLE_API_TOKEN", "--secret", "SECOND_SECRET", "--working-directory", scratch, "--", probe, .. target];
    private static void StatusIs(RunResult result, Status status) => Assert(result.Status == status, "STATUS_" + (int)result.Status);
    private static string Metadata() => JsonSerializer.Serialize(new
    {
        executable = probe, arguments = new[] { "exchange" }, workingDirectory = scratch,
        secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "" }, new { name = "SECOND_SECRET", description = "" } }
    });
    private static void RequestContract()
    {
        string file = Path.Combine(scratch, "request.json"); File.WriteAllText(file, Metadata());
        var snapshot = RequestLoader.Load(file); File.WriteAllText(file, "{}");
        Assert(snapshot.Arguments.Single() == "exchange"); Reject(() => RequestLoader.Load(file));
        foreach (string content in new[] { "not-json", Metadata().Replace("\"arguments\":", "\"unknown\":0,\"arguments\":"),
            Metadata().Replace("\"arguments\":", "\"arguments\":[],\"arguments\":"), new string(' ', 65537) })
        { File.WriteAllText(file, content); Reject(() => RequestLoader.Load(file)); }
    }
    private static void CliContract()
    {
        string[] supplied = Args(Vector);
        var parsed = LaunchOptions.Parse(supplied, scratch).DirectRequest!;
        Assert(parsed.Arguments.SequenceEqual(Vector));
        supplied[^1] = "changed"; Assert(parsed.Arguments[^1] == "--");
        foreach (var bad in new string[][] {
            ["--watch-caller", .. Args()], ["--watch-parent", "123", "456", .. Args()],
            ["--help", .. Args()], ["--request", "file", .. Args()], ["--secret", "PATH", "--", probe],
            ["--secret", "A", "--secret", "a", "--", probe], ["--description", "x", .. Args()],
            ["--secret", "A", "--", "relative.exe"], ["--working-directory", "", .. Args()],
            ["--secret", "A", "--", probe, "nul\0arg"], ["--secret", "A", "--"]
        }) Reject(() => LaunchOptions.Parse(bad, scratch));
        Assert(LaunchOptions.Parse(["--secret", "NODE_AUTH_TOKEN", "--working-directory", ".", "--", probe], scratch).DirectRequest!.WorkingDirectory == scratch);
        Assert(LaunchOptions.Parse(["--request", "metadata"]).RequestPath == "metadata");
    }
    private static void EnvironmentContract()
    {
        foreach (string name in new[] { "GIT_API_TOKEN", "NODE_AUTH_TOKEN", "SSH_PASSWORD", "OP_SERVICE_ACCOUNT_TOKEN" })
            Assert(EnvironmentPolicy.AllowedName(name));
        foreach (string name in new[] { "PATH", "NODE_OPTIONS", "NAME\n", "A=B", "" }) Assert(!EnvironmentPolicy.AllowedName(name));
        foreach (string value in new[] { "", "x\0y", "x\ny", new string('x', 16385) }) Assert(!EnvironmentPolicy.ValidValue(value));
        Assert(EnvironmentPolicy.ValidValue(" x \u03A9 "));
        string? previous = Environment.GetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN", "synthetic-ambient");
            char[] block = EnvironmentPolicy.Build(Request(), Values());
            var keys = new string(block).Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(s => s[..s.IndexOf('=')]).ToArray();
            Assert(!keys.Contains("SHANDOFF_FOREIGN_TOKEN") && keys.Contains("EXAMPLE_API_TOKEN"));
            Array.Clear(block);
        }
        finally { Environment.SetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN", previous); }
    }
    private static void Paths()
    {
        string spaced = Path.Combine(scratch, "path \u03A9"); Directory.CreateDirectory(spaced);
        Assert(RequestLoader.Create(probe.Replace('\\', '/'), [], Path.Combine(spaced, ".."), [new("A", "")]).WorkingDirectory == scratch);
        Reject(() => RequestLoader.Create(probe + ":stream", [], scratch, [new("A", "")]));
        Reject(() => RequestLoader.Create(probe, [], "C:relative", [new("A", "")]));
        Reject(() => RequestLoader.Create(probe, [], scratch, [new("A", "hidden\u202e")]));
        string junction = Path.Combine(scratch, "linked");
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("/c"); info.ArgumentList.Add("mklink"); info.ArgumentList.Add("/J"); info.ArgumentList.Add(junction); info.ArgumentList.Add(spaced);
        using var p = Process.Start(info)!;
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        Assert(p.WaitForExit(5000) && p.ExitCode == 0, "JUNCTION_SETUP");
        Assert(RequestLoader.Create(probe, [], junction, [new("A", "")]).WorkingDirectory == junction);
    }
    private static Process Start(string[] args)
    {
        var info = new ProcessStartInfo(launcher) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = scratch };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }
    private static (Task<string> Output, Task<string> Error) Read(Process p) => (p.StandardOutput.ReadToEndAsync(), p.StandardError.ReadToEndAsync());
    private static void End(Process p) { if (!p.HasExited) { p.Kill(); Assert(p.WaitForExit(5000), "FIXTURE_CLEANUP"); } }
    private static void Help()
    {
        foreach (var item in new[] { (new[] { "--help" }, 0), (new[] { "--unknown", "SYNTHETIC_METADATA_NOT_TO_ECHO" }, 5) })
        {
            using var p = Start(item.Item1); var capture = Read(p);
            try
            {
                Assert(p.WaitForExit(5000) && p.ExitCode == item.Item2, "CLI_EXIT");
                if (item.Item2 == 0) Assert(capture.Output.Result.Contains("Output contract: passthrough") && capture.Error.Result == "");
                else Assert(capture.Output.Result == "" && capture.Error.Result.StartsWith("INVALID_REQUEST:") && !capture.Error.Result.Contains("SYNTHETIC_METADATA_NOT_TO_ECHO"));
            }
            finally { End(p); }
        }
    }
    private static void Input()
    {
        string? clipboard = "  synthetic-\u03A9  "; int errors = 0;
        using var input = new SecretInput(() => clipboard, _ => errors++);
        Assert(input.UseSystemPasswordChar);
        input.PasteValue(); Assert(input.Text == clipboard);
        input.SelectAll(); clipboard = "replacement"; input.PasteValue(); Assert(input.Text == clipboard);
        clipboard = "invalid\nline"; input.PasteValue(); Assert(input.Text == "replacement" && errors == 1);
        Assert(input.HandleSecretKey(Keys.Control | Keys.C) && input.HandleSecretKey(Keys.Enter));
    }
    private static void BeforeStart()
    {
        string marker = Path.Combine(scratch, "before-start");
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); var values = Values();
        StatusIs(new WindowsProcessRunner().RunAsync(Request("wait", marker), values, cancel.Token).Result, Status.Cancelled);
        Assert(!File.Exists(marker) && values.Count == 0);
        values = Values(); values["SECOND_SECRET"] = "invalid\n";
        StatusIs(new WindowsProcessRunner().RunAsync(Request("wait", marker), values, default).Result, Status.InvalidRequest);
        Assert(!File.Exists(marker) && values.Count == 0);
    }
    private static void CancelStarting()
    {
        string marker = Path.Combine(scratch, "starting");
        using var cancel = new CancellationTokenSource();
        var runner = new WindowsProcessRunner { CreatedForTest = _ => cancel.Cancel() };
        StatusIs(runner.RunAsync(Request("wait", marker), Values(), cancel.Token).Result, Status.Cancelled);
        Assert(!File.Exists(marker));
    }
    private static void StopDirect()
    {
        string marker = Path.Combine(scratch, "stop");
        Process? target = null;
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runner = new WindowsProcessRunner { CreatedForTest = id => { target = Process.GetProcessById((int)id); _ = target.SafeHandle; } };
        var task = runner.RunAsync(Request("wait", marker), Values(), cancel.Token);
        try { WaitFile(marker); cancel.Cancel(); StatusIs(task.Result, Status.Cancelled); Assert(target!.WaitForExit(1000)); }
        finally { cancel.Cancel(); task.Wait(6000); if (target != null) { End(target); target.Dispose(); } }
    }
    private static void StartFailure()
    {
        string invalid = Path.Combine(scratch, "not-a-program.exe"); File.WriteAllText(invalid, "synthetic-invalid-executable");
        var request = RequestLoader.Create(invalid, [], scratch, [new("EXAMPLE_API_TOKEN", ""), new("SECOND_SECRET", "")]);
        var values = Values();
        StatusIs(new WindowsProcessRunner().RunAsync(request, values, default).Result, Status.StartFailed);
        Assert(values.Count == 0);
    }
    private static void HandleInheritance()
    {
        var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        using var sentinel = CreateEvent(ref security, true, false, null);
        Assert(!sentinel.IsInvalid, "SENTINEL_SETUP");
        StatusIs(new WindowsProcessRunner().RunAsync(Request("handle-check", sentinel.DangerousGetHandle().ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)), Values(), default).Result, Status.Completed);
    }
    private static void Descendants()
    {
        string marker = Path.Combine(scratch, "descendant"); Process? target = null, child = null;
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var runner = new WindowsProcessRunner { CreatedForTest = id => { target = Process.GetProcessById((int)id); _ = target.SafeHandle; } };
        var task = runner.RunAsync(Request("tree", marker), Values(), cancel.Token);
        try
        {
            WaitFile(marker);
            string[] metadata = File.ReadAllText(marker).Split(' ');
            var candidate = Process.GetProcessById(int.Parse(metadata[0]));
            try
            {
                _ = candidate.SafeHandle;
                Assert(candidate.StartTime.ToUniversalTime().ToFileTimeUtc() == long.Parse(metadata[1]) && !candidate.HasExited, "CHILD_IDENTITY");
                Assert(QueryProcess(candidate.SafeHandle, 0, out var basic, Marshal.SizeOf<BasicInformation>(), out _) == 0 && basic.Parent.ToInt64() == target!.Id, "CHILD_PARENT");
                var image = new StringBuilder(32768); uint size = (uint)image.Capacity;
                Assert(QueryImage(candidate.SafeHandle, 0, image, ref size) && string.Equals(image.ToString(), probe, StringComparison.OrdinalIgnoreCase), "CHILD_IMAGE");
                child = candidate;
            }
            finally { if (child == null) candidate.Dispose(); }
            File.WriteAllText(marker + ".release", "");
            Assert(task.Wait(5000), "DIRECT_WAIT"); StatusIs(task.Result, Status.Completed);
            Assert(!child.WaitForExit(200), "DESCENDANT_TERMINATED");
        }
        finally { cancel.Cancel(); task.Wait(6000); if (child != null) { End(child); child.Dispose(); } if (target != null) { End(target); target.Dispose(); } }
    }
    private static void FormOnce()
    {
        int count = 0; var completion = new TaskCompletionSource<RunResult>(); Dictionary<string,string>? owned = null;
        using var form = new ApprovalForm(Request(), () => null, (_, values, _) => { count++; owned = values; return completion.Task; });
        form.Show(); Application.DoEvents();
        Assert(!form.StartButton.Enabled);
        FillLocal(form); var pending = form.StartApprovedAsync(); form.StartApprovedAsync().GetAwaiter().GetResult();
        Assert(count == 1 && form.Inputs.All(i => i.Text == ""));
        completion.SetResult(new RunResult(Status.Completed)); Pump(pending);
        Assert(form.IsDisposed && owned!.Count == 0 && form.Result.Status == Status.Completed, "FORM_AUTO_CLOSE");
    }
    private static void FormDisposal()
    {
        string marker = Path.Combine(scratch, "form-stop"); Process? target = null;
        var runner = new WindowsProcessRunner { CreatedForTest = id => { target = Process.GetProcessById((int)id); _ = target.SafeHandle; } };
        using var form = new ApprovalForm(Request("wait", marker), () => null, runner.RunAsync);
        form.Show(); Application.DoEvents(); FillLocal(form); var task = form.StartApprovedAsync();
        try { WaitFile(marker); form.Dispose(); Assert(target!.WaitForExit(1000)); StatusIs(form.Result, Status.Cancelled); }
        finally { form.Dispose(); if (target != null) { End(target); target.Dispose(); } }
    }
    private static void FillLocal(ApprovalForm form) { form.Inputs[0].Text = "synthetic-demo-one"; form.Inputs[1].Text = "synthetic-demo-two"; }
    private static void Pump(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(10); }
        Assert(task.IsCompleted, "UI_TIMEOUT"); task.GetAwaiter().GetResult();
    }
    private static void WaitFile(string path)
    {
        var watch = Stopwatch.StartNew();
        while (!File.Exists(path) && watch.ElapsedMilliseconds < 5000) Thread.Sleep(10);
        Assert(File.Exists(path), "MARKER_TIMEOUT");
    }
    private static void GuiResult(string mode, int code, string output, string error)
    {
        using var p = Start(Args(mode)); var capture = Read(p);
        try
        {
            Approve(p); Assert(p.WaitForExit(10000) && p.ExitCode == code, "GUI_RESULT_EXIT");
            Assert(capture.Output.Result == output && capture.Error.Result == error, "RESULT_CHANNELS");
            Assert(!capture.Output.Result.Contains("synthetic-demo-one") && !capture.Error.Result.Contains("synthetic-demo-one"), "INPUT_EXPOSED");
        }
        finally { End(p); }
    }
    private static void Flood()
    {
        using var p = Start(Args("flood"));
        using var stdout = new MemoryStream(); using var stderr = new MemoryStream();
        var a = p.StandardOutput.BaseStream.CopyToAsync(stdout); var b = p.StandardError.BaseStream.CopyToAsync(stderr);
        try
        {
            Approve(p); Assert(p.WaitForExit(15000) && p.ExitCode == 0, "FLOOD_EXIT");
            Assert(Task.WaitAll([a,b], 5000), "FLOOD_DRAIN");
            foreach (var stream in new[] { stdout, stderr })
            {
                byte[] data = stream.ToArray(); Assert(data.Length == 256 * 16384, "FLOOD_LENGTH");
                for (int i = 0; i < data.Length; i++) Assert(data[i] == (byte)i, "BINARY_CHANGED");
            }
        }
        finally { End(p); }
    }
    private static void Roundtrip()
    {
        string? previous = Environment.GetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN");
        string? options = Environment.GetEnvironmentVariable("NODE_OPTIONS");
        try
        {
            Environment.SetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN", "synthetic-ambient");
            Environment.SetEnvironmentVariable("NODE_OPTIONS", "synthetic-control");
            using var p = Start(Args(["roundtrip", scratch, .. Vector])); var capture = Read(p);
            try
            {
                Approve(p, " synthetic-only-\u03A9\\\" ", "synthetic-two");
                Assert(p.WaitForExit(10000) && p.ExitCode == 0, "ROUNDTRIP_EXIT");
                Assert(capture.Output.Result == "ROUNDTRIP_OK\r\n" && capture.Error.Result == "");
                Assert(Environment.GetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN") == "synthetic-ambient");
            }
            finally { End(p); }
        }
        finally { Environment.SetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN", previous); Environment.SetEnvironmentVariable("NODE_OPTIONS", options); }
    }
    private static void CancelGui()
    {
        string marker = Path.Combine(scratch, "gui-cancel");
        using var p = Start(Args("wait", marker)); var capture = Read(p);
        try { Cancel(p); Assert(p.WaitForExit(5000) && p.ExitCode == 2 && !File.Exists(marker)); Assert(capture.Output.Result == "" && capture.Error.Result == ""); }
        finally { End(p); }
    }
    private static void ConcurrentGui()
    {
        using var first = Start(Args("exchange")); using var second = Start(Args("exchange"));
        var a = Read(first); var b = Read(second);
        try { Controls(first); Approve(second); Assert(second.WaitForExit(10000) && second.ExitCode == 0); Assert(!first.HasExited); Cancel(first); Assert(first.WaitForExit(5000) && first.ExitCode == 2); }
        finally { End(first); End(second); }
    }
    private static void JsonGui()
    {
        string path = Path.Combine(scratch, "compatibility.json"); File.WriteAllText(path, Metadata());
        using var p = Start(["--request", path]); var capture = Read(p);
        try { Approve(p); Assert(p.WaitForExit(10000) && p.ExitCode == 0); Assert(capture.Output.Result == "synthetic-result-credential\r\n"); }
        finally { End(p); }
    }
    private static void Repeated()
    {
        using var self = Process.GetCurrentProcess(); self.Refresh(); int before = self.HandleCount;
        for (int i = 0; i < 8; i++) StatusIs(new WindowsProcessRunner().RunAsync(Request("quiet"), Values(), default).Result, Status.Completed);
        self.Refresh(); Assert(self.HandleCount <= before + 8, "HANDLE_GROWTH");
    }

    // Only handles returned by our own Process.Start reach these UI mutation helpers.
    private static void Owned(Process p, IntPtr h)
    {
        Assert(!p.HasExited && Wait(p.SafeHandle, 0) == 258 && WindowOwner(h, out uint id) != 0 && id == p.Id, "WINDOW_NOT_OWNED");
    }
    private static List<IntPtr> Controls(Process p)
    {
        var watch = Stopwatch.StartNew();
        string previous = ""; int stable = 0;
        while (!p.HasExited && watch.ElapsedMilliseconds < 6000)
        {
            p.Refresh();
            if (p.MainWindowHandle != IntPtr.Zero)
            {
                Owned(p, p.MainWindowHandle);
                var list = new List<IntPtr>();
                EnumChildWindows(p.MainWindowHandle, (h, _) => { list.Add(h); return true; }, IntPtr.Zero);
                var inputs = Inputs(list);
                string layout = string.Join(",", inputs.Select(h => { Rect(h, out var r); return r.Top + ":" + r.Left + ":" + r.Bottom; }));
                if (inputs.Length == 2 && inputs.All(Visible) && layout == previous &&
                    list.Any(h => Class(h).Contains("BUTTON", StringComparison.OrdinalIgnoreCase) && Caption(h) == "Start"))
                { if (++stable >= 3) return list; }
                else stable = 0;
                previous = layout;
            }
            Thread.Sleep(30);
        }
        throw new Failure("GUI_NOT_READY");
    }
    private static IntPtr[] Inputs(List<IntPtr> list) => list.Where(h => Class(h).StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase) && (Style(h, -16) & 4) == 0)
        .OrderBy(h => { Rect(h, out var r); return r.Top; }).ToArray();
    private static void Message(Process p, IntPtr h, uint message, string value)
    {
        Owned(p, h); Assert(Send(h, message, IntPtr.Zero, value, 0x22, 1000, out _) != IntPtr.Zero, "GUI_MESSAGE_FAILED");
    }
    private static void Approve(Process p, string first = "synthetic-demo-one", string second = "synthetic-demo-two")
    {
        var controls = Controls(p); var inputs = Inputs(controls);
        Message(p, inputs[0], 0xC, first); Message(p, inputs[1], 0xC, second);
        var start = controls.Single(h => Class(h).Contains("BUTTON", StringComparison.OrdinalIgnoreCase) && Caption(h) == "Start");
        Message(p, start, 0xF5, "");
    }
    private static void Cancel(Process p)
    {
        var controls = Controls(p); var cancel = controls.Single(h => Class(h).Contains("BUTTON", StringComparison.OrdinalIgnoreCase) && Caption(h) == "Cancel");
        Message(p, cancel, 0xF5, "");
    }
    private static string Class(IntPtr h) { var b = new StringBuilder(256); GetClass(h,b,b.Capacity); return b.ToString(); }
    private static string Caption(IntPtr h) { var b = new StringBuilder(256); GetCaption(h,b,b.Capacity); return b.ToString(); }
    private delegate bool Enumerate(IntPtr h, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { internal int Length; internal IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] internal bool Inherit; }
    [DllImport("kernel32.dll", EntryPoint="CreateEventW", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern SafeWaitHandle CreateEvent(ref SecurityAttributes security, [MarshalAs(UnmanagedType.Bool)] bool manual, [MarshalAs(UnmanagedType.Bool)] bool initial, string? name);
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInformation { internal IntPtr Exit, Peb, Affinity, Priority, Id, Parent; }
    [DllImport("ntdll.dll", EntryPoint="NtQueryInformationProcess")] private static extern int QueryProcess(SafeProcessHandle p, int kind, out BasicInformation info, int size, out int returned);
    [DllImport("kernel32.dll", EntryPoint="WaitForSingleObject")] private static extern uint Wait(SafeProcessHandle p,uint ms);
    [DllImport("kernel32.dll", EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode)] private static extern bool QueryImage(SafeProcessHandle p,uint flags,StringBuilder path,ref uint size);
    [DllImport("user32.dll", EntryPoint="IsWindowVisible")] private static extern bool Visible(IntPtr h);
    [DllImport("user32.dll", EntryPoint="GetWindowThreadProcessId")] private static extern uint WindowOwner(IntPtr h,out uint id);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr h, Enumerate callback, IntPtr data);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int Style(IntPtr h,int index);
    [DllImport("user32.dll", EntryPoint="GetWindowRect")] private static extern bool Rect(IntPtr h,out Rectangle r);
    [DllImport("user32.dll", EntryPoint="GetClassNameW",CharSet=CharSet.Unicode)] private static extern int GetClass(IntPtr h,StringBuilder b,int n);
    [DllImport("user32.dll", EntryPoint="GetWindowTextW",CharSet=CharSet.Unicode)] private static extern int GetCaption(IntPtr h,StringBuilder b,int n);
    [DllImport("user32.dll", EntryPoint="SendMessageTimeoutW",CharSet=CharSet.Unicode)] private static extern IntPtr Send(IntPtr h,uint msg,IntPtr w,string value,uint flags,uint timeout,out UIntPtr result);
}
