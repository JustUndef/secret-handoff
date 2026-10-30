using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static SecretHandoff.Core.NativeMethods;

namespace SecretHandoff.Core;

public sealed class WindowsProcessRunner
{
    internal Action<uint>? CreatedForTest { get; init; }
    internal Action<uint>? StartedForTest { get; init; }

    // Values are owned by this call and cleared on every path.
    public Task<RunResult> RunAsync(LaunchRequest request, Dictionary<string, string> values, CancellationToken cancellation) =>
        Task.Run(() => Run(request, values, cancellation));

    private RunResult Run(LaunchRequest request, Dictionary<string, string> values, CancellationToken cancellation)
    {
        KernelHandle? process = null, thread = null;
        var result = new RunResult(Status.InternalError);
        try
        {
            request.Revalidate();
            if (cancellation.IsCancellationRequested) return new RunResult(Status.Cancelled);
            using var input = OpenNull(0x80000000);
            using var output = DuplicateOutput(-11);
            using var error = DuplicateOutput(-12);
            using var attributes = new ProcessAttributes(input, output, error);
            using var environment = new EnvironmentBlock(EnvironmentPolicy.Build(request, values));
            values.Clear();
            var startup = new StartupInfoEx
            {
                Startup = new StartupInfo
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                    Input = input.DangerousGetHandle(), Output = output.DangerousGetHandle(), Error = error.DangerousGetHandle()
                },
                Attributes = attributes.Pointer
            };
            if (cancellation.IsCancellationRequested) return new RunResult(Status.Cancelled);
            request.Revalidate();
            // Suspend only to close the approval/cancellation race before target code executes.
            Check(CreateProcessW(request.Executable, new StringBuilder(WindowsArguments.Encode(request.Executable, request.Arguments)),
                IntPtr.Zero, IntPtr.Zero, true, 0x80000 | 0x400 | 0x08000000 | 0x4,
                environment.Pointer, request.WorkingDirectory, ref startup, out var info));
            process = new KernelHandle(info.Process);
            thread = new KernelHandle(info.Thread);
            environment.Dispose();
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
                    Check(wait == 258);
                    if (cancellation.IsCancellationRequested) { result = new RunResult(Status.Cancelled); break; }
                }
            }
        }
        catch (RequestException) { result = new RunResult(Status.InvalidRequest); }
        catch (Win32Exception e) { result = new RunResult(process == null ? Status.StartFailed : Status.InternalError, WindowsError: e.NativeErrorCode); }
        catch { result = new RunResult(Status.InternalError); }
        finally
        {
            values.Clear();
            // Explicit local cancellation/errors affect only our directly created process.
            // Normal completion never terminates descendants; no job or caller watcher exists.
            if (process != null && result.Status is not (Status.Completed or Status.TargetFailed) && !StopDirect(process))
                result = new RunResult(Status.StopFailed);
            thread?.Dispose();
            process?.Dispose();
        }
        return result;
    }

    private static bool StopDirect(KernelHandle process)
    {
        if (WaitForSingleObject(process, 0) == 0) return true;
        if (!TerminateProcess(process, 2) && WaitForSingleObject(process, 0) != 0) return false;
        return WaitForSingleObject(process, 5000) == 0;
    }

    private static KernelHandle DuplicateOutput(int channel)
    {
        IntPtr original = GetStdHandle(channel);
        // Explorer/manual launches may have no output recipient.
        if (original == IntPtr.Zero || original == new IntPtr(-1)) return OpenNull(0x40000000);
        Check(DuplicateHandle(new IntPtr(-1), original, new IntPtr(-1), out var duplicate, 0, true, 2));
        return duplicate;
    }

    private static KernelHandle OpenNull(uint access)
    {
        var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        var handle = CreateFileW("NUL", access, 3, ref security, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { int code = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(code); }
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
        internal EnvironmentBlock(char[] chars)
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
        private IntPtr handles;
        private bool initialized;
        internal ProcessAttributes(params KernelHandle[] inherited)
        {
            try
            {
                nuint bytes = 0;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref bytes);
                if (bytes == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                Pointer = Marshal.AllocHGlobal(checked((nint)bytes));
                Check(InitializeProcThreadAttributeList(Pointer, 1, 0, ref bytes));
                initialized = true;
                handles = Marshal.AllocHGlobal(IntPtr.Size * inherited.Length);
                for (int i = 0; i < inherited.Length; i++) Marshal.WriteIntPtr(handles, i * IntPtr.Size, inherited[i].DangerousGetHandle());
                Check(UpdateProcThreadAttribute(Pointer, 0, 0x20002, handles, (nuint)(IntPtr.Size * inherited.Length), IntPtr.Zero, IntPtr.Zero));
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (initialized) { DeleteProcThreadAttributeList(Pointer); initialized = false; }
            if (Pointer != IntPtr.Zero) { Marshal.FreeHGlobal(Pointer); Pointer = IntPtr.Zero; }
            if (handles != IntPtr.Zero) { Marshal.FreeHGlobal(handles); handles = IntPtr.Zero; }
        }
    }
}
