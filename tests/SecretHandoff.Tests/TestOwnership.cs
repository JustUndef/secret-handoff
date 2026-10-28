using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SecretHandoff.Core;

internal static partial class Program
{
    private static string DemoExecutable => Path.GetFullPath(Path.Combine(root, "artifacts/demo/SecretHandoff.exe"));

    // A Process returned by Start already owns its process handle. Enumeration alone grants no
    // ownership: pin each candidate before querying it and compare native data through that handle.
    // The retained handle is also used by Process.Kill/WaitForExit in the callers' cleanup paths.
    private static Process? WaitForOwnedWrapperGui(Process wrapper)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 6000 && IsLiveTestProcess(wrapper))
        {
            Process? owned = null;
            bool accepted = false;
            var candidates = Process.GetProcessesByName("SecretHandoff");
            try
            {
                foreach (var candidate in candidates)
                {
                    if (!IsOwnedWrapperGui(candidate, wrapper)) continue;
                    Assert(owned == null, "WRAPPER_MULTIPLE_OWNED_GUI");
                    owned = candidate;
                }
                if (owned != null && IsOwnedWrapperGui(owned, wrapper)) { accepted = true; return owned; }
                owned = null;
            }
            finally
            {
                foreach (var candidate in candidates)
                    if (!accepted || !ReferenceEquals(candidate, owned)) candidate.Dispose();
            }
            Thread.Sleep(20);
        }
        return null;
    }

    private static bool IsLiveTestProcess(Process process)
    {
        try
        {
            // Do not reopen by PID after this point. A signalled, invalid or unqueryable handle
            // is a refusal, including exit and PID reuse races during enumeration.
            var handle = process.SafeHandle;
            return OwnershipWaitForSingleObject(handle, 0) == 0x102 &&
                OwnershipGetProcessId(handle) == (uint)process.Id;
        }
        catch (Win32Exception) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static bool IsOwnedWrapperGui(Process candidate, Process wrapper, string? expectedPath = null)
    {
        try
        {
            if (!IsLiveTestProcess(wrapper) || !IsLiveTestProcess(candidate)) return false;
            var childHandle = candidate.SafeHandle;
            var wrapperHandle = wrapper.SafeHandle;
            if (!OwnershipGetProcessTimes(childHandle, out long childCreated, out _, out _, out _) ||
                !OwnershipGetProcessTimes(wrapperHandle, out long wrapperCreated, out _, out _, out _) ||
                childCreated < wrapperCreated) return false;
            if (OwnershipQueryProcess(childHandle, 0, out var basic, Marshal.SizeOf<OwnershipBasicInformation>(), out _) != 0 ||
                basic.ParentId.ToInt64() != wrapper.Id || basic.ProcessId.ToInt64() != candidate.Id) return false;
            var path = new StringBuilder(32768);
            uint length = (uint)path.Capacity;
            if (!OwnershipQueryFullProcessImageName(childHandle, 0, path, ref length) ||
                !string.Equals(path.ToString(), expectedPath ?? DemoExecutable, StringComparison.OrdinalIgnoreCase)) return false;
            return IsLiveTestProcess(wrapper) && IsLiveTestProcess(candidate);
        }
        catch (Win32Exception) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static void AssertWindowOwned(Process process, IntPtr window, Process? wrapperOwner = null)
    {
        Assert(IsLiveTestProcess(process), "GUI_PROCESS_ENDED");
        if (wrapperOwner != null) Assert(IsOwnedWrapperGui(process, wrapperOwner), "GUI_WRAPPER_OWNERSHIP_LOST");
        Assert(window != IntPtr.Zero && OwnershipGetWindowThreadProcessId(window, out uint id) != 0 &&
            id == (uint)process.Id, "GUI_WINDOW_OWNERSHIP_LOST");
        Assert(IsLiveTestProcess(process), "GUI_PROCESS_ENDED");
    }

    private static UIntPtr SendOwnedGuiMessage(Process process, IntPtr window, uint message, string value, Process? wrapperOwner = null)
    {
        AssertWindowOwned(process, window, wrapperOwner);
        // SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT: never retry with an enumerated replacement.
        Assert(SendMessageTimeoutW(window, message, IntPtr.Zero, value, 0x22, 1000, out var result) != IntPtr.Zero,
            "GUI_MESSAGE_FAILED");
        return result;
    }

    private static List<IntPtr> WaitOwnedGuiControls(Process process)
    {
        var watch = Stopwatch.StartNew();
        string previousLayout = "";
        int stableSamples = 0;
        while (IsLiveTestProcess(process) && watch.ElapsedMilliseconds < 5000)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                // Readiness is established by owned WinForms controls, also for console EXEs.
                AssertWindowOwned(process, process.MainWindowHandle);
                var controls = Children(process.MainWindowHandle);
                var edits = controls.Where(h => ClassName(h).StartsWith("WindowsForms10.Edit", StringComparison.OrdinalIgnoreCase) &&
                    (GetWindowLongW(h, -16) & 0x4) == 0).ToArray();
                string layout = string.Join(",", edits.Select(h =>
                {
                    GetWindowRect(h, out var rect);
                    return rect.Top + ":" + rect.Bottom + ":" + rect.Left;
                }).Order());
                if (edits.Length == 2 && controls.Any(h => Caption(h) == "Start") &&
                    edits.All(OwnershipIsWindowVisible) && layout == previousLayout)
                {
                    if (++stableSamples >= 3) return controls;
                }
                else stableSamples = 0;
                previousLayout = layout;
            }
            Thread.Sleep(20);
        }
        throw new TestFailure("OWNED_GUI_NOT_SHOWN");
    }

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OwnershipIsWindowVisible(IntPtr window);

    private static Process StartOwnershipWrapper(string request)
    {
        string shell = Environment.GetEnvironmentVariable("SHANDOFF_TEST_POWERSHELL") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/powershell.exe");
        var info = new ProcessStartInfo(shell)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "scripts/Invoke-Handoff.ps1"), "-RequestPath", request })
            info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }

    private static Process StartOwnershipFirstGui(string request)
    {
        using var self = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(DemoExecutable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in new[] { "--request", request, "--watch-parent",
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Creation(self).ToString(System.Globalization.CultureInfo.InvariantCulture) }) info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }

    private static void AssertOwnershipGuiUntouched(Process first, List<IntPtr> controls, string marker)
    {
        Assert(IsLiveTestProcess(first), "OTHER_OWNED_GUI_TERMINATED");
        var inputs = controls.Where(h => ClassName(h).StartsWith("WindowsForms10.Edit", StringComparison.OrdinalIgnoreCase) &&
            (GetWindowLongW(h, -16) & 0x4) == 0).ToArray();
        Assert(inputs.Length == 2, "OWNERSHIP_INPUT_COUNT");
        foreach (var input in inputs)
            Assert(SendOwnedGuiMessage(first, input, 0xE, "").ToUInt64() == 0, "OTHER_OWNED_GUI_FILLED"); // WM_GETTEXTLENGTH only
        var start = controls.Single(h => Caption(h) == "Start");
        AssertWindowOwned(first, start);
        Assert(!OwnershipIsWindowEnabled(start), "OTHER_OWNED_GUI_START_ENABLED");
        Assert(!File.Exists(marker) && !File.Exists(marker + ".child"), "OTHER_OWNED_GUI_STARTED");
    }

    private static void WrapperBusyPreservesOtherInstance()
    {
        string marker = Path.Combine(scratch, "ownership-first");
        string secondMarker = Path.Combine(scratch, "ownership-second");
        using var first = StartOwnershipFirstGui(TreeRequest(marker));
        Process? ownSecond = null;
        try
        {
            var controls = WaitOwnedGuiControls(first);
            AssertOwnershipGuiUntouched(first, controls, marker);
            AssertPidOnlyDriverDisabled(first);
            AssertOwnershipGuiUntouched(first, controls, marker);
            using var wrapper = StartOwnershipWrapper(TreeRequest(secondMarker));
            var output = wrapper.StandardOutput.ReadToEndAsync();
            var error = wrapper.StandardError.ReadToEndAsync();
            try
            {
                // Run the same discovery path as all three wrapper drivers. A brief BUSY
                // child may be identified; the existing first instance must never be returned.
                ownSecond = WaitForOwnedWrapperGui(wrapper);
                Assert(ownSecond == null || ownSecond.Id != first.Id, "OTHER_GUI_ATTACHED");
                if (ownSecond != null)
                {
                    Assert(ownSecond.WaitForExit(5000), "BUSY_GUI_REMAINED");
                    Assert(ownSecond.ExitCode == (int)Status.Busy, "BUSY_GUI_EXIT");
                }
                Assert(wrapper.WaitForExit(5000) && wrapper.ExitCode == (int)Status.Busy, "BUSY_WRAPPER_EXIT");
                const string hostDiagnostic = "Attempting to perform the InitializeDefaultDrives operation on the 'FileSystem' provider failed.";
                Assert(output.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim() == "HANDOFF_EXIT_4" &&
                    error.GetAwaiter().GetResult().Replace(hostDiagnostic, "", StringComparison.Ordinal).Trim() == "", "BUSY_WRAPPER_OUTPUT");
                AssertOwnershipGuiUntouched(first, controls, marker);
                Assert(!File.Exists(secondMarker) && !File.Exists(secondMarker + ".child"), "BUSY_TARGET_STARTED");
            }
            finally { if (!wrapper.HasExited) { wrapper.Kill(); wrapper.WaitForExit(5000); } }
        }
        finally
        {
            // Only Process.Start handles or a fully verified wrapper child can reach cleanup.
            if (ownSecond != null) { if (!ownSecond.HasExited) { ownSecond.Kill(); ownSecond.WaitForExit(5000); } ownSecond.Dispose(); }
            if (!first.HasExited) { first.Kill(); first.WaitForExit(5000); }
        }
    }

    private static void AssertPidOnlyDriverDisabled(Process first)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("--drive-synthetic-gui");
        info.ArgumentList.Add(first.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var driver = Process.Start(info)!;
        var output = driver.StandardOutput.ReadToEndAsync(); var error = driver.StandardError.ReadToEndAsync();
        try
        {
            Assert(driver.WaitForExit(5000) && driver.ExitCode == 1, "PID_DRIVER_NOT_DISABLED");
            Assert(output.GetAwaiter().GetResult().Trim() == "SYNTHETIC_GUI_DRIVER_DISABLED" &&
                error.GetAwaiter().GetResult() == "", "PID_DRIVER_OUTPUT");
        }
        finally { if (!driver.HasExited) { driver.Kill(); driver.WaitForExit(5000); } }
    }

    private static void WrapperOwnershipRejectsOtherAndExitedOwner()
    {
        using var owner = StartLifetimeOwner();
        string marker = Path.Combine(scratch, "ownership-unrelated");
        // Same expected GUI executable, created after owner: neither EXE path nor temporal
        // ordering can reject this candidate. Its actual parent relationship must do so.
        using var other = StartOwnershipFirstGui(TreeRequest(marker));
        try
        {
            var controls = WaitOwnedGuiControls(other);
            Assert(!IsOwnedWrapperGui(other, owner), "UNRELATED_PROCESS_ACCEPTED");
            owner.Kill(); Assert(owner.WaitForExit(5000));
            Assert(!IsOwnedWrapperGui(other, owner), "EXITED_OWNER_ACCEPTED");
            Assert(WaitForOwnedWrapperGui(owner) == null, "EXITED_OWNER_DISCOVERED_GUI");
            AssertOwnershipGuiUntouched(other, controls, marker);
            other.Kill(); Assert(other.WaitForExit(5000));
            Assert(!IsLiveTestProcess(other) && !IsOwnedWrapperGui(other, owner), "EXITED_CANDIDATE_ACCEPTED");
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(5000); }
            if (!other.HasExited) { other.Kill(); other.WaitForExit(5000); }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OwnershipBasicInformation
    {
        internal IntPtr Reserved1, Peb, Reserved2, Reserved3, ProcessId, ParentId;
    }
    [DllImport("kernel32.dll", EntryPoint = "WaitForSingleObject")]
    private static extern uint OwnershipWaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("kernel32.dll", EntryPoint = "GetProcessId")]
    private static extern uint OwnershipGetProcessId(SafeProcessHandle process);
    [DllImport("kernel32.dll", EntryPoint = "GetProcessTimes")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OwnershipGetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OwnershipQueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);
    [DllImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
    private static extern int OwnershipQueryProcess(SafeProcessHandle process, int informationClass, out OwnershipBasicInformation information, int size, out int returned);
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint OwnershipGetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", EntryPoint = "IsWindowEnabled")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OwnershipIsWindowEnabled(IntPtr window);
}
