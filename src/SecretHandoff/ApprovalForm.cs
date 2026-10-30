
using System.Text.Json;
using SecretHandoff.Core;

namespace SecretHandoff;

internal sealed class ApprovalForm : Form
{
    private readonly LaunchRequest request;
    private readonly ApprovalLifecycle lifecycle;
    private bool running, terminal, lifecycleDisposed;
    internal List<SecretInput> Inputs { get; } = [];
    internal List<Button> PasteButtons { get; } = [];
    internal Button StartButton { get; } = new() { Text = "Start", AutoSize = true, Enabled = false };
    internal Button CancelButtonControl { get; } = new() { Text = "Cancel", AutoSize = true };
    internal Label MessageLabel { get; } = new() { AutoSize = true, MaximumSize = new Size(850, 0) };
    internal RunResult Result { get; private set; } = new(Status.Cancelled);
    internal string Preview { get; }

    internal ApprovalForm(LaunchRequest request,
        Func<string?>? clipboard = null,
        Func<LaunchRequest, Dictionary<string, string>, CancellationToken, Task<RunResult>>? run = null)
    {
        this.request = request;
        lifecycle = new ApprovalLifecycle( run ?? new WindowsProcessRunner().RunAsync);
        clipboard ??= () => Clipboard.ContainsText() ? Clipboard.GetText() : null;
        Text = "Secret Handoff – Review request";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 580);
        Size = new Size(960, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            Text = "Enter values locally for the operation you approve",
            AutoSize = true, ForeColor = Color.Maroon, Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        Preview = "Request: " + request.SourcePath + "\r\n\r\nProgram: " + request.Executable +
            "\r\nWorking directory: " + request.WorkingDirectory + "\r\n\r\nArguments (individual):\r\n" +
            (request.Arguments.Length == 0 ? "(none)" : string.Join("\r\n", request.Arguments.Select((a, i) =>
                $"[{i}] {JsonSerializer.Serialize(a)}")));
        var preview = new TextBox
        {
            Text = Preview, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = SystemColors.Window,
            AccessibleName = "Complete target invocation", TabStop = true
        };
        layout.Controls.Add(preview, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "Output from the approved program is returned to the caller, including intentionally retrieved credentials.\r\n" +
                "The program is trusted with your input. No execution time limit.\r\n" +
                "Stop/closing this window stops only the direct program. Agent Stop may leave it running.",
            AutoSize = true, Margin = new Padding(0, 10, 0, 10)
        }, 0, 2);
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new Padding(0, 0, 18, 0) };
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(entries, 0, 3);
        int row = 0;
        foreach (var spec in request.Secrets)
        {
            var label = new Label { Text = spec.Name + " — " + spec.Description, AutoSize = true, Margin = new Padding(0, 10, 0, 4) };
            entries.Controls.Add(label, 0, row++);
            entries.SetColumnSpan(label, 2);
            var input = new SecretInput(clipboard, ShowInputError) { Dock = DockStyle.Top, AccessibleName = spec.Name };
            input.TextChanged += (_, _) => RefreshValidity();
            var paste = new Button { Text = "Paste", AutoSize = true, AccessibleName = "Paste: " + spec.Name };
            paste.Click += (_, _) => input.PasteValue();
            entries.Controls.Add(input, 0, row);
            entries.Controls.Add(paste, 1, row++);
            Inputs.Add(input);
            PasteButtons.Add(paste);
        }
        layout.Controls.Add(MessageLabel, 0, 4);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(CancelButtonControl);
        buttons.Controls.Add(StartButton);
        layout.Controls.Add(buttons, 0, 5);
        StartButton.Click += async (_, _) => await StartApprovedAsync();
        CancelButtonControl.Click += (_, _) => CancelOrClose();
        CancelButton = CancelButtonControl;
        // Deliberately no AcceptButton: Enter in an input never grants approval.
        Shown += (_, _) => Inputs[0].Focus();
        FormClosing += (_, e) =>
        {
            if (running)
            {
                e.Cancel = true;

                lifecycle.Cancel();
                MessageLabel.Text = "Stopping the direct program …";
            }
            else ClearInputs();
        };
        RefreshValidity();
    }

    private void RefreshValidity()
    {
        if (running || terminal) return;
        StartButton.Enabled = Inputs.Count == request.Secrets.Length && Inputs.All(i => EnvironmentPolicy.ValidValue(i.Text));
        MessageLabel.Text = StartButton.Enabled ? "Review the request and explicitly select Start." : "Enter all required values.";
    }

    private void ShowInputError(string text) => MessageLabel.Text = text;

    internal async Task StartApprovedAsync()
    {
        if (running || terminal) return;

        RefreshValidity();
        if (!StartButton.Enabled) return;
        running = true;
        StartButton.Enabled = false;
        CancelButtonControl.Text = "Stop task";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < Inputs.Count; i++) values.Add(request.Secrets[i].Name, Inputs[i].Text);
        ClearInputs();
        MessageLabel.Text = "Task running. Output is returned to the caller.";
        Result = await lifecycle.StartAsync(request, values);
        running = false;
        // Disposal can finish the runner while this continuation is still queued in WinForms.
        if (IsDisposed || Disposing || lifecycleDisposed) return;
        Finish(Result);

    }

    internal void CancelOrClose()
    {
        if (running)
        {
            lifecycle.Cancel();
            MessageLabel.Text = "Stopping the direct program …";
        }
        else Close();
    }

    private void Finish(RunResult result)
    {
        Result = result;
        terminal = true;
        ClearInputs();
        StartButton.Enabled = false;
        CancelButtonControl.Text = "Close";
        MessageLabel.Text = StatusText.Category(result.Status) + " · " + StatusText.Describe(result.Status) +
            (result.TargetExitCode is uint target ? $" Target exit code: {target}." : "") +
            (result.WindowsError is int error ? $" Windows error code: {error}." : "");
        Close();
    }

    private void ClearInputs()
    {
        foreach (var input in Inputs) { input.Clear(); input.Enabled = false; }
        foreach (var button in PasteButtons) button.Enabled = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !lifecycleDisposed)
        {
            // Stop the direct target before destroying controls or reporting a UI failure.
            Result = lifecycle.StopAndWait(Result);
            lifecycleDisposed = true;
            running = false;
            terminal = true;
            ClearInputs();
            lifecycle.Dispose();
        }
        base.Dispose(disposing);
    }
}
