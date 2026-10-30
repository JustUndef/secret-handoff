using SecretHandoff.Core;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("SecretHandoff.Tests")]

namespace SecretHandoff;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool commandLine = args.Length != 0;
        try
        {
            var options = LaunchOptions.Parse(args);
            if (options.Help) { Console.Out.WriteLine(CliHelp.Text); return 0; }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            var request = options.DirectRequest;
            if (request == null && options.RequestPath == null)
            {
                using var picker = new OpenFileDialog
                {
                    Title = "Secret Handoff – Select request", Filter = "Handoff request (*.json)|*.json|All files (*.*)|*.*",
                    AddToRecent = false, CheckFileExists = true, RestoreDirectory = true
                };
                if (picker.ShowDialog() != DialogResult.OK) return (int)Status.Cancelled;
                request = RequestLoader.Load(picker.FileName);
            }
            else if (request == null) request = RequestLoader.Load(options.RequestPath!);
            return (int)RunApprovalSession(new ApprovalForm(request)).Status;
        }
        catch (CliException e) { WriteDiagnostic("INVALID_REQUEST: " + e.Message); return (int)Status.InvalidRequest; }
        catch (RequestException)
        {
            if (commandLine) WriteDiagnostic("INVALID_REQUEST: " + StatusText.Describe(Status.InvalidRequest));
            else MessageBox.Show(StatusText.Describe(Status.InvalidRequest), "Secret Handoff");
            return (int)Status.InvalidRequest;
        }
        catch
        {
            if (commandLine) WriteDiagnostic("INTERNAL_ERROR: Request ended.");
            else MessageBox.Show("Request ended.", "Secret Handoff");
            return (int)Status.InternalError;
        }
    }

    private static void WriteDiagnostic(string text)
    {
        try { Console.Error.WriteLine(text); } catch { }
    }

    internal static RunResult RunApprovalSession(ApprovalForm form, Action<RunResult>? reportUiFailure = null)
    {
        bool failed = false;
        try { Application.Run(form); }
        catch { failed = true; }
        finally { try { form.Dispose(); } catch { failed = true; } }
        var result = form.Result;
        if (failed && result.Status != Status.StopFailed) result = new RunResult(Status.InternalError);
        if (failed) { try { reportUiFailure?.Invoke(result); } catch { } }
        return result;
    }
}
