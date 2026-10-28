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
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        ApplicationConfiguration.Initialize();
        bool supervised = args.Contains("--watch-parent", StringComparer.Ordinal);
        try
        {
            string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException();
            using var single = new Mutex(false, @"Local\SecretHandoff." + sid);
            bool acquired;
            try { acquired = single.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return (int)Status.Busy;
            try
            {
                var options = LaunchOptions.Parse(args);
                using var parent = options.ParentId is uint parentId
                    ? new ParentProcessWatch(parentId, options.ParentCreation!.Value) : null;
                if (parent?.Terminated.IsCancellationRequested == true) return (int)Status.Cancelled;
                string path;
                if (options.RequestPath is null)
                {
                    using var picker = new OpenFileDialog
                    {
                        Title = "Secret Handoff – Auftrag wählen",
                        Filter = "Handoff-Auftrag (*.json)|*.json|Alle Dateien (*.*)|*.*",
                        AddToRecent = false, CheckFileExists = true, RestoreDirectory = true
                    };
                    if (picker.ShowDialog() != DialogResult.OK) return (int)Status.Cancelled;
                    path = picker.FileName;
                }
                else path = options.RequestPath;
                var request = RequestLoader.Load(path);
                var form = new ApprovalForm(request, parentTermination: parent?.Terminated ?? default);
                return (int)RunApprovalSession(form, reportUiFailure: supervised ? null : ShowUiFailure).Status;
            }
            finally { single.ReleaseMutex(); }
        }
        catch (ParentWatchException e) { return (int)e.Status; }
        catch (RequestException)
        {
            if (!supervised)
                MessageBox.Show(StatusText.Describe(Status.InvalidRequest), "Secret Handoff", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return (int)Status.InvalidRequest;
        }
        catch
        {
            // Never expose raw exception messages, command output, or crash uploads.
            if (!supervised)
                ShowUiFailure(new RunResult(Status.InternalError));
            return (int)Status.InternalError;
        }
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
