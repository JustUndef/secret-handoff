using System.Diagnostics;
using SecretHandoff;
using SecretHandoff.Core;
using HandoffProgram = SecretHandoff.Program;

internal static partial class Program
{
    private static void RegisterUiFailureTests()
    {
        Test("ui-loop-failure-before-approval", UiLoopFailureBeforeApproval);
        Test("ui-loop-failure-cleans-before-report", () => UiLoopFailureDuringRun(false));
        Test("ui-loop-failure-cleanup-status-priority", () => UiLoopFailureDuringRun(true));
        Test("form-dispose-joins-independent-cleanup", () => UiDisposeDuringRun(false));
        Test("form-dispose-cleanup-status-priority", () => UiDisposeDuringRun(true));
    }

    private static void UiLoopFailureBeforeApproval()
    {
        int starts = 0, reports = 0;
        bool reportedAfterCleanup = false;
        var form = new ApprovalForm(Request("fail"), () => null, (_, values, _) =>
        {
            starts++;
            values.Clear();
            return Task.FromResult(new RunResult(Status.Completed));
        });
        form.Shown += (_, _) => throw new InvalidOperationException();
        var result = HandoffProgram.RunApprovalSession(form, reportUiFailure: reported =>
        {
            reports++;
            Assert(form.IsDisposed && starts == 0, "UI_FAILURE_EARLY_REPORT");
            AssertResult(reported, Status.InternalError);
            reportedAfterCleanup = true;
        });
        AssertResult(result, Status.InternalError);
        Assert(starts == 0 && reports == 1 && reportedAfterCleanup, "UI_FAILURE_APPROVAL_BYPASS");
    }

    private static void UiLoopFailureDuringRun(bool cleanupFailure)
    {
        string marker = Path.Combine(scratch, "ui-loop-failure-" + cleanupFailure);
        var runner = new WindowsProcessRunner { FailCleanupForTest = cleanupFailure };
        var form = new ApprovalForm(Request("tree", marker, "wait"), () => null, runner.RunAsync);
        bool armed = false;
        bool reportedAfterCleanup = false;
        int reports = 0;
        form.Shown += (_, _) =>
        {
            foreach (var input in form.Inputs) input.Text = "synthetic-ui-failure";
            form.StartButton.PerformClick();
            // No message pumping: both native processes must be alive at the actual UI exception.
            WaitMarker(marker);
            AssertUiTreeAlive(marker);
            armed = true;
            throw new InvalidOperationException();
        };
        Status expected = cleanupFailure ? Status.CleanupFailed : Status.InternalError;
        var result = HandoffProgram.RunApprovalSession(form, reportUiFailure: reported =>
        {
            reports++;
            Assert(armed && form.IsDisposed, "UI_FAILURE_REPORT_BEFORE_DISPOSE");
            AssertUiTreeAlreadyDead(marker);
            AssertResult(reported, expected);
            reportedAfterCleanup = true;
            if (cleanupFailure) throw new InvalidOperationException(); // A failing error display cannot hide CLEANUP_FAILED.
        });
        Assert(armed && reports == 1 && reportedAfterCleanup, "UI_FAILURE_NOT_INJECTED");
        AssertUiTreeAlreadyDead(marker);
        AssertResult(result, expected);
        // Drain any UI continuation left behind by Application.Run's failure. It cannot change cleanup status.
        Application.DoEvents();
        AssertResult(form.Result, cleanupFailure ? Status.CleanupFailed : Status.Cancelled);
    }

    private static void UiDisposeDuringRun(bool cleanupFailure)
    {
        string marker = Path.Combine(scratch, "ui-dispose-" + cleanupFailure);
        var runner = new WindowsProcessRunner { FailCleanupForTest = cleanupFailure };
        bool contextFree = false;
        async Task<RunResult> RunWithoutUiDependency(LaunchRequest request, Dictionary<string, string> values, CancellationToken token)
        {
            contextFree = SynchronizationContext.Current == null;
            // This await would capture WinForms and deadlock Dispose if the lifecycle invoked us on its context.
            await Task.Yield();
            return await runner.RunAsync(request, values, token);
        }
        using var form = new ApprovalForm(Request("tree", marker, "wait"), () => null, RunWithoutUiDependency);
        form.Show(); Application.DoEvents();
        foreach (var input in form.Inputs) input.Text = "synthetic-ui-dispose";
        var pendingUi = form.StartApprovedAsync();
        try
        {
            WaitMarker(marker);
            AssertUiTreeAlive(marker);
            // This synchronous production Dispose must complete without pumping a single UI message.
            form.Dispose();
            Assert(contextFree && form.IsDisposed, "UI_DISPOSE_CONTEXT_DEPENDENCY");
            AssertUiTreeAlreadyDead(marker);
            Assert(form.Inputs.All(input => input.TextLength == 0), "UI_DISPOSE_INPUTS_RETAINED");
            AssertResult(form.Result, cleanupFailure ? Status.CleanupFailed : Status.Cancelled);
            Pump(pendingUi);
            AssertResult(form.Result, cleanupFailure ? Status.CleanupFailed : Status.Cancelled);
        }
        finally { form.Dispose(); }
    }

    private static void AssertUiTreeAlive(string marker)
    {
        foreach (string file in new[] { marker, marker + ".child" })
        {
            int id = int.Parse(File.ReadAllText(file), System.Globalization.CultureInfo.InvariantCulture);
            using var process = Process.GetProcessById(id);
            Assert(!process.HasExited, "UI_FAILURE_TREE_NOT_RUNNING");
        }
    }

    private static void AssertUiTreeAlreadyDead(string marker)
    {
        // An immediate check makes the ordering assertion stronger than waiting for eventual exit.
        foreach (string file in new[] { marker, marker + ".child" })
        {
            int id = int.Parse(File.ReadAllText(file), System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                using var process = Process.GetProcessById(id);
                Assert(process.WaitForExit(0), "UI_FAILURE_REPORTED_BEFORE_CLEANUP");
            }
            catch (ArgumentException) { /* Already gone. */ }
        }
    }
}
