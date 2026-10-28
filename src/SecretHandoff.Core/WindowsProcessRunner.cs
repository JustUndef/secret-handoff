using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static SecretHandoff.Core.NativeMethods;

namespace SecretHandoff.Core;

public sealed class WindowsProcessRunner
{
    private readonly TimeSpan runtimeLimit;
    private readonly LocalPathPolicy pathPolicy;
    internal Action<uint>? CreatedForTest { get; init; }
    internal Action<uint>? StartedForTest { get; init; }
    internal bool FailCleanupForTest { get; init; }

    public WindowsProcessRunner() : this(TimeSpan.FromMinutes(15)) { }
    internal WindowsProcessRunner(TimeSpan runtimeLimit) : this(runtimeLimit, LocalPathPolicy.Native) { }
    internal WindowsProcessRunner(TimeSpan runtimeLimit, LocalPathPolicy pathPolicy)
    {
        this.runtimeLimit = runtimeLimit;
        this.pathPolicy = pathPolicy;
    }

    // Takes ownership of values. Caller must not mutate/read them after this call.
    public Task<RunResult> RunAsync(LaunchRequest request, Dictionary<string, string> values, CancellationToken cancellation) =>
        Task.Run(() => Run(request, values, cancellation));

    private RunResult Run(LaunchRequest request, Dictionary<string, string> values, CancellationToken cancellation)
    {
        KernelHandle? job = null, process = null, thread = null;
        var result = new RunResult(Status.InternalError);
        bool created = false;
        try
        {
            request.Revalidate(pathPolicy);
            if (cancellation.IsCancellationRequested) return new RunResult(Status.Cancelled);
            job = CreateJobObjectW(IntPtr.Zero, null);
            Check(!job.IsInvalid);
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } }; // KILL_ON_JOB_CLOSE
            Check(SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()));
            using var input = OpenNull(0x80000000); // GENERIC_READ
            using var output = OpenNull(0x40000000); // GENERIC_WRITE
            using var attributes = new ProcessAttributes(job, input, output);
            using var environment = new EnvironmentBlock(EnvironmentPolicy.Build(request, values));
            values.Clear();
            var startup = new StartupInfoEx
            {
                Startup = new StartupInfo
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100, // STARTF_USESTDHANDLES
                    Input = input.DangerousGetHandle(), Output = output.DangerousGetHandle(), Error = output.DangerousGetHandle()
                },
                Attributes = attributes.Pointer
            };
            if (cancellation.IsCancellationRequested) return new RunResult(Status.Cancelled);
            // Assign the job atomically at creation. Suspended start also permits a cancellation recheck before execution.
            request.Revalidate(pathPolicy);
            Check(CreateProcessW(request.Executable, new StringBuilder(WindowsArguments.Encode(request.Executable, request.Arguments)),
                IntPtr.Zero, IntPtr.Zero, true, 0x80000 | 0x400 | 0x08000000 | 0x4,
                environment.Pointer, request.WorkingDirectory, ref startup, out var info));
            created = true;
            process = new KernelHandle(info.Process);
            thread = new KernelHandle(info.Thread);
            environment.Dispose(); // Wipe our native copy before allowing the target to execute.
            var elapsed = Stopwatch.StartNew();
            CreatedForTest?.Invoke(info.ProcessId);
            if (cancellation.IsCancellationRequested) result = new RunResult(Status.Cancelled);
            else
            {
                Check(ResumeThread(thread) != uint.MaxValue);
                StartedForTest?.Invoke(info.ProcessId);
                while (true)
                {
                    uint wait = WaitForSingleObject(process, 25);
                    if (wait == 0)
                    {
                        Check(GetExitCodeProcess(process, out uint exitCode));
                        result = new RunResult(exitCode == 0 ? Status.Completed : Status.TargetFailed, exitCode);
                        break;
                    }
                    Check(wait == 258); // WAIT_TIMEOUT; all other results are failures.
                    if (cancellation.IsCancellationRequested) { result = new RunResult(Status.Cancelled); break; }
                    if (elapsed.Elapsed >= runtimeLimit) { result = new RunResult(Status.TimedOut); break; }
                }
            }
        }
        catch (RequestException) { result = new RunResult(Status.InvalidRequest); }
        catch (Win32Exception e) { result = new RunResult(created ? Status.InternalError : Status.StartFailed, WindowsError: e.NativeErrorCode); }
        catch { result = new RunResult(Status.InternalError); }
        finally
        {
            values.Clear();
            if (created && job is { IsInvalid: false })
            {
                // Never return success/cancellation while descendants are still running.
                bool clean = CleanJob(job);
                if (!clean || FailCleanupForTest) result = new RunResult(Status.CleanupFailed);
            }
            thread?.Dispose();
            process?.Dispose();
            job?.Dispose(); // Last handle closes even when cleanup verification fails.
        }
        return result;
    }

    private static bool CleanJob(KernelHandle job)
    {
        try
        {
            if (!TerminateJobObject(job, 2)) return false;
            var watch = Stopwatch.StartNew();
            do
            {
                if (!QueryInformationJobObject(job, 1, out var accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero)) return false;
                if (accounting.ActiveProcesses == 0) return true;
                Thread.Sleep(10);
            } while (watch.Elapsed < TimeSpan.FromSeconds(5));
        }
        catch { /* Sanitized status only. */ }
        return false;
    }

    private static KernelHandle OpenNull(uint access)
    {
        var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        var handle = CreateFileW("NUL", access, 3, ref security, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private sealed class EnvironmentBlock : IDisposable
    {
        public IntPtr Pointer { get; private set; }
        private readonly int bytes;

        public EnvironmentBlock(char[] chars)
        {
            bytes = checked(chars.Length * sizeof(char));
            try { Pointer = Marshal.AllocHGlobal(bytes); Marshal.Copy(chars, 0, Pointer, chars.Length); }
            finally { Array.Clear(chars); }
        }

        public unsafe void Dispose()
        {
            if (Pointer == IntPtr.Zero) return;
            CryptographicOperations.ZeroMemory(new Span<byte>(Pointer.ToPointer(), bytes));
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
        }
    }

    private sealed class ProcessAttributes : IDisposable
    {
        public IntPtr Pointer { get; private set; }
        private IntPtr handles, jobs;
        private bool initialized;

        public ProcessAttributes(KernelHandle job, KernelHandle input, KernelHandle output)
        {
            try
            {
                nuint bytes = 0;
                InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref bytes);
                if (bytes == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                Pointer = Marshal.AllocHGlobal(checked((nint)bytes));
                Check(InitializeProcThreadAttributeList(Pointer, 2, 0, ref bytes));
                initialized = true;
                handles = Marshal.AllocHGlobal(IntPtr.Size * 2);
                Marshal.WriteIntPtr(handles, input.DangerousGetHandle());
                Marshal.WriteIntPtr(handles, IntPtr.Size, output.DangerousGetHandle());
                jobs = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(jobs, job.DangerousGetHandle());
                Check(UpdateProcThreadAttribute(Pointer, 0, 0x20002, handles, (nuint)(IntPtr.Size * 2), IntPtr.Zero, IntPtr.Zero));
                Check(UpdateProcThreadAttribute(Pointer, 0, 0x2000D, jobs, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (initialized) { DeleteProcThreadAttributeList(Pointer); initialized = false; }
            if (Pointer != IntPtr.Zero) { Marshal.FreeHGlobal(Pointer); Pointer = IntPtr.Zero; }
            if (handles != IntPtr.Zero) { Marshal.FreeHGlobal(handles); handles = IntPtr.Zero; }
            if (jobs != IntPtr.Zero) { Marshal.FreeHGlobal(jobs); jobs = IntPtr.Zero; }
        }
    }
}
