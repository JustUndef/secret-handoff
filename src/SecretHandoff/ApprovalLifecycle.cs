using SecretHandoff.Core;

namespace SecretHandoff;

// Owns the process task independently of the WinForms message loop and its continuations.
internal sealed class ApprovalLifecycle : IDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource cancellation;
    private readonly Func<LaunchRequest, Dictionary<string, string>, CancellationToken, Task<RunResult>> run;
    private Task<RunResult>? execution;
    private bool started, stopping, disposed;

    internal ApprovalLifecycle(
        Func<LaunchRequest, Dictionary<string, string>, CancellationToken, Task<RunResult>> run)
    {
        cancellation = new CancellationTokenSource();
        this.run = run;
    }

    internal Task<RunResult> StartAsync(LaunchRequest request, Dictionary<string, string> values)
    {
        lock (gate)
        {
            if (stopping || started)
            {
                values.Clear();
                return execution ?? Task.FromResult(new RunResult(Status.Cancelled));
            }
            // Only this task owns values after approval. No UI continuation is required to clear them.
            started = true;
            execution = RunOwnedAsync(request, values);
            return execution;
        }
    }

    private async Task<RunResult> RunOwnedAsync(LaunchRequest request, Dictionary<string, string> values)
    {
        try
        {
            Task<RunResult> processTask;
            var context = SynchronizationContext.Current;
            try
            {
                // The runner must never acquire a dependency on a UI context during invocation either.
                SynchronizationContext.SetSynchronizationContext(null);
                processTask = run(request, values, cancellation.Token);
            }
            finally { SynchronizationContext.SetSynchronizationContext(context); }
            return await processTask.ConfigureAwait(false);
        }
        catch { return new RunResult(Status.InternalError); }
        finally { values.Clear(); }
    }

    internal void Cancel()
    {
        lock (gate)
        {
            if (!disposed)
            {
                try { cancellation.Cancel(); }
                catch (AggregateException) { /* Still join the process task if a cancellation callback failed. */ }
            }
        }
    }

    internal RunResult StopAndWait(RunResult beforeStart)
    {
        Task<RunResult>? pending;
        lock (gate)
        {
            stopping = true;
            Cancel();
            pending = execution;
        }
        // Wait only for the runner and its cleanup, never for StartApprovedAsync's UI continuation.
        return pending == null ? beforeStart : pending.GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        StopAndWait(new RunResult(Status.Cancelled));
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            cancellation.Dispose();
        }
    }
}
