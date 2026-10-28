using System.Diagnostics;
using System.Security.Cryptography;
using SecretHandoff.Core;

internal static partial class Program
{
    // Dispatch before UI setup/test registration. This host has no test-summary output:
    // only its exit code crosses the boundary unless output suppression regresses.
    private static bool TryRunOutputDiscardHost(string[] args, out int exitCode)
    {
        exitCode = 71;
        if (args.Length == 0 || (args[0] != "--output-discard-host" && args[0] != "--output-detector-control"))
            return false;
        try
        {
            if (args.Length == 1 && args[0] == "--output-detector-control")
            {
                // Public synthetic binary fixtures, generated here rather than supplied
                // through argv/environment. The parent must detect both channels.
                using var stdout = Console.OpenStandardOutput();
                using var stderr = Console.OpenStandardError();
                stdout.Write([0, 255, 65, 0]);
                stderr.Write([0, 254, 66, 0]);
                stdout.Flush();
                stderr.Flush();
                exitCode = 0;
            }
            else if (args.Length == 2 && args[0] == "--output-discard-host")
            {
                probe = args[1];
                // Keep the production NUL handles unchanged. This limit only shortens
                // the synthetic runner's lifecycle; no output-policy test switch exists.
                var result = new WindowsProcessRunner(TimeSpan.FromSeconds(10))
                    .RunAsync(Request("flood"), Values(), CancellationToken.None).GetAwaiter().GetResult();
                exitCode = result.Status == Status.Completed && result.TargetExitCode == 0 ? 0 : 72;
            }
        }
        catch { exitCode = 73; } // Never allow runtime exceptions/diagnostics to escape the host.
        return true;
    }

    private static void OutputFloodDiscarded()
    {
        var captured = CaptureOutputHost("--output-discard-host", probe);
        Assert(captured.ExitCode == 0, "OUTPUT_HOST_FAILED");
        Assert(IsSilent(captured.StdoutBytes), "STDOUT_NOT_DISCARDED");
        Assert(IsSilent(captured.StderrBytes), "STDERR_NOT_DISCARDED");
    }

    private static void OutputDetectorNegativeControl()
    {
        // Use the identical capture/drain path, bypassing only the production runner.
        // Exact byte counts also catch a detector that reads only one channel.
        var captured = CaptureOutputHost("--output-detector-control");
        Assert(captured.ExitCode == 0, "OUTPUT_CONTROL_HOST_FAILED");
        Assert(!IsSilent(captured.StdoutBytes) && captured.StdoutBytes == 4, "STDOUT_DETECTOR_MISSED_CONTROL");
        Assert(!IsSilent(captured.StderrBytes) && captured.StderrBytes == 4, "STDERR_DETECTOR_MISSED_CONTROL");
    }

    private static bool IsSilent(long bytes) => bytes == 0;

    private static (int ExitCode, long StdoutBytes, long StderrBytes) CaptureOutputHost(params string[] arguments)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using var drainCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var host = Process.Start(info) ?? throw new TestFailure("OUTPUT_HOST_NOT_STARTED");
        Stream? stdout = null, stderr = null;
        Task<long[]>? drains = null;
        try
        {
            // Retain the process handle: cleanup acts on this exact child, never a
            // PID looked up later, a name match, or the surrounding test runner.
            _ = host.SafeHandle;
            host.StandardInput.Close();
            stdout = host.StandardOutput.BaseStream;
            stderr = host.StandardError.BaseStream;
            Task<long> stdoutDrain = CountAndDiscardAsync(stdout, drainCancellation.Token);
            Task<long> stderrDrain = CountAndDiscardAsync(stderr, drainCancellation.Token);
            drains = Task.WhenAll(stdoutDrain, stderrDrain);
            // The target has a ten-second runner limit plus its five-second cleanup
            // budget. The separate host watchdog also covers deadlock/runner hangs.
            Assert(host.WaitForExit(25000), "OUTPUT_HOST_TIMEOUT");
            long[] counts;
            try { counts = drains.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            catch { throw new TestFailure("OUTPUT_DRAIN_FAILED"); }
            return (host.ExitCode, counts[0], counts[1]);
        }
        finally
        {
            bool stopped = true;
            try
            {
                if (!host.HasExited)
                {
                    // Nonrecursive kill of our retained host. Closing that host's
                    // last job handle terminates its synthetic target if it hangs.
                    host.Kill();
                    stopped = host.WaitForExit(5000);
                }
            }
            catch { stopped = false; }
            finally
            {
                drainCancellation.Cancel();
                stdout?.Dispose();
                stderr?.Dispose();
                // Cancellation and pipe disposal release pending reads even if a
                // regressed child retained a pipe. Never wait without a deadline.
                try { drains?.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); }
                catch { /* Counts/raw bytes and exceptions never leave the capture. */ }
            }
            Assert(stopped, "OUTPUT_HOST_CLEANUP_FAILED");
        }
    }

    private static async Task<long> CountAndDiscardAsync(Stream stream, CancellationToken cancellation)
    {
        // Two fixed 4-KiB buffers, no decoding, strings, output copies or files.
        // Keep draining after the first byte so a forwarding regression cannot
        // deadlock the 32-MiB/binary SyntheticProbe flood on a full pipe.
        byte[] buffer = new byte[4096];
        long count = 0;
        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false);
                if (read == 0) return count;
                count += read;
                CryptographicOperations.ZeroMemory(buffer);
            }
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }
}
