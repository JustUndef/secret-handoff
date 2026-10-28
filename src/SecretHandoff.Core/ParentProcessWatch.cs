using System.Runtime.InteropServices;
using static SecretHandoff.Core.NativeMethods;

namespace SecretHandoff.Core;

public sealed class ParentWatchException(Status status) : Exception("Caller monitoring unavailable.")
{
    public Status Status { get; } = status;
}

// Liveness only: neither the supplied PID nor its creation time grants authority over secrets.
public sealed class ParentProcessWatch : IDisposable
{
    private readonly KernelHandle process;
    private readonly CancellationTokenSource ended = new();
    private readonly Task watcher;
    private int disposed;

    public CancellationToken Terminated => ended.Token;

    public ParentProcessWatch(uint processId, long creationTime)
    {
        if (processId == 0 || processId == Environment.ProcessId || creationTime <= 0) throw new RequestException();
        // No memory access, termination right, debug privilege or inheritable handle.
        process = OpenProcess(0x00100000 | 0x1000, false, processId); // SYNCHRONIZE | QUERY_LIMITED_INFORMATION
        try
        {
            if (process.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                throw new ParentWatchException(error == 87 ? Status.Cancelled : Status.StartFailed);
            }
            if (!GetProcessTimes(process, out long actualCreation, out _, out _, out _))
                throw new ParentWatchException(Status.StartFailed);
            if (creationTime != actualCreation) throw new RequestException();
            // Reuse a pool worker instead of creating a native thread handle per watch.
            watcher = Task.Run(Watch);
        }
        catch { process.Dispose(); ended.Dispose(); throw; }
    }

    private void Watch()
    {
        while (Volatile.Read(ref disposed) == 0)
        {
            uint wait = WaitForSingleObject(process, 50);
            if (wait == 258) continue; // WAIT_TIMEOUT
            // A signalled process or a failed wait both stop the request (fail closed).
            ended.Cancel();
            return;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // The process handle must outlive the wait. The wait is bounded to 50 ms.
        watcher.GetAwaiter().GetResult();
        process.Dispose();
        ended.Dispose();
    }
}
