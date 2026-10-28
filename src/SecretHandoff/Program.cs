using SecretHandoff.Core;
using System.Runtime.CompilerServices;
using System.Security.Principal;

[assembly: InternalsVisibleTo("SecretHandoff.Tests")]

namespace SecretHandoff;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool supervised = false;
        bool commandLine = args.Length != 0;
        try
        {
            var options = LaunchOptions.Parse(args); // Captures caller CWD before any UI.
            if (options.Help)
            {
                Console.Out.WriteLine(CliHelp.Text);
                return 0;
            }
            supervised = options.ParentId != null;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException();
            using var single = new Mutex(false, @"Local\SecretHandoff." + sid);
            bool acquired;
            try { acquired = single.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return (int)Status.Busy;
            try
            {
                using var parent = options.ParentId is uint parentId
                    ? new ParentProcessWatch(parentId, options.ParentCreation!.Value) : null;
                if (parent?.Terminated.IsCancellationRequested == true) return (int)Status.Cancelled;
                var request = options.DirectRequest;
                if (request is null && options.RequestPath is null)
                {
                    using var picker = new OpenFileDialog
                    {
                        Title = "Secret Handoff – Select request",
                        Filter = "Handoff request (*.json)|*.json|All files (*.*)|*.*",
                        AddToRecent = false, CheckFileExists = true, RestoreDirectory = true
                    };
                    if (picker.ShowDialog() != DialogResult.OK) return (int)Status.Cancelled;
                    request = RequestLoader.Load(picker.FileName);
                }
                else if (request is null) request = RequestLoader.Load(options.RequestPath!);
                var form = new ApprovalForm(request, parentTermination: parent?.Terminated ?? default);
                return (int)RunApprovalSession(form, reportUiFailure: commandLine || supervised ? null : ShowUiFailure).Status;
            }
            finally { single.ReleaseMutex(); }
        }
        catch (CliException e)
        {
            WriteDiagnostic("INVALID_REQUEST: " + e.Message); // Own fixed strings only.
            return (int)Status.InvalidRequest;
        }
        catch (ParentWatchException e)
        {
            if (commandLine) WriteDiagnostic(StatusText.Category(e.Status) + ": " + StatusText.Describe(e.Status));
            return (int)e.Status;
        }
        catch (RequestException)
        {
            if (commandLine) WriteDiagnostic("INVALID_REQUEST: " + StatusText.Describe(Status.InvalidRequest) + " Help: --help.");
            else if (!supervised)
                MessageBox.Show(StatusText.Describe(Status.InvalidRequest), "Secret Handoff", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return (int)Status.InvalidRequest;
        }
        catch
        {
            // Never expose raw exception messages, command output, or crash uploads.
            if (commandLine) WriteDiagnostic("INTERNAL_ERROR: " + StatusText.Describe(Status.InternalError));
            else if (!supervised)
                ShowUiFailure(new RunResult(Status.InternalError));
            return (int)Status.InternalError;
        }
    }

    private static void WriteDiagnostic(string text)
    {
        try { Console.Error.WriteLine(text); }
        catch { /* Missing/closed terminal must not replace the fixed exit code. */ }
    }

    internal static RunResult RunApprovalSession(ApprovalForm form, Action<RunResult>? reportUiFailure = null)
    {
        bool uiFailed = false;
        try { Application.Run(form); }
        catch { uiFailed = true; }
        finally
        {
            // Dispose stops and joins the UI-independent lifecycle even if Application.Run threw.
            try { form.Dispose(); }
            catch { uiFailed = true; }
        }
        var result = form.Result;
        if (uiFailed && result.Status != Status.CleanupFailed) result = new RunResult(Status.InternalError);
        if (uiFailed)
        {
            // An unavailable result dialog must not replace the already confirmed cleanup result.
            try { reportUiFailure?.Invoke(result); }
            catch { /* Fixed status only; no second UI attempt or raw diagnostics. */ }
        }
        return result;
    }

    private static void ShowUiFailure(RunResult result) =>
        MessageBox.Show(StatusText.Category(result.Status) + " · " + StatusText.Describe(result.Status),
            "Secret Handoff", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
