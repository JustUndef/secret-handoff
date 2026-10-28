using System.Diagnostics;
using System.Text.Json;
using SecretHandoff.Core;

namespace SecretHandoff;

internal sealed class ApprovalForm : Form
{
    private readonly LaunchRequest request;
    private readonly ApprovalLifecycle lifecycle;
    private readonly CancellationToken parentTermination;
    private CancellationTokenRegistration parentRegistration;
    private readonly System.Windows.Forms.Timer approvalTimer = new() { Interval = 500 };
    private readonly Stopwatch approvalWatch = new();
    private readonly TimeSpan approvalLimit;
    private bool running, terminal, closingAfterRun, lifecycleDisposed;
    internal List<SecretInput> Inputs { get; } = [];
    internal List<Button> PasteButtons { get; } = [];
    internal Button StartButton { get; } = new() { Text = "Starten", AutoSize = true, Enabled = false };
    internal Button CancelButtonControl { get; } = new() { Text = "Abbrechen", AutoSize = true };
    internal Label MessageLabel { get; } = new() { AutoSize = true, MaximumSize = new Size(850, 0) };
    internal RunResult Result { get; private set; } = new(Status.Cancelled);
    internal string Preview { get; }

    internal ApprovalForm(LaunchRequest request,
        Func<string?>? clipboard = null,
        Func<LaunchRequest, Dictionary<string, string>, CancellationToken, Task<RunResult>>? run = null,
        TimeSpan? approvalLimit = null,
        CancellationToken parentTermination = default)
    {
        this.request = request;
        lifecycle = new ApprovalLifecycle(parentTermination, run ?? new WindowsProcessRunner().RunAsync);
        this.approvalLimit = approvalLimit ?? TimeSpan.FromMinutes(5);
        this.parentTermination = parentTermination;
        clipboard ??= () => Clipboard.ContainsText() ? Clipboard.GetText() : null;
        Text = "Secret Handoff – Auftrag prüfen";
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
            Text = "Prototyp · ausschließlich synthetische Testwerte verwenden",
            AutoSize = true, ForeColor = Color.Maroon, Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        Preview = "Auftrag: " + request.SourcePath + "\r\n\r\nProgramm: " + request.Executable +
            "\r\nArbeitsverzeichnis: " + request.WorkingDirectory + "\r\n\r\nArgumente (einzeln):\r\n" +
            (request.Arguments.Length == 0 ? "(keine)" : string.Join("\r\n", request.Arguments.Select((a, i) =>
                $"[{i}] {JsonSerializer.Serialize(a)}")));
        var preview = new TextBox
        {
            Text = Preview, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = SystemColors.Window,
            AccessibleName = "Vollständiger Zielaufruf", TabStop = true
        };
        layout.Controls.Add(preview, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "Freigabe: 5 Minuten · Laufzeit: höchstens 15 Minuten. Ausgaben werden verworfen.\r\n" +
                "Nur minimale Windows-Umgebung plus Ihre Eingaben. Ziel und Nachkommen können die Werte lesen und weitergeben.\r\n" +
                "Abbrechen beendet den betreuten Prozessbaum; beim Zielende werden verbleibende Nachkommen beendet." +
                (parentTermination.CanBeCanceled ? "\r\nEndet der überwachte Aufrufer, wird dieser Auftrag automatisch beendet." : ""),
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
            var paste = new Button { Text = "Einfügen", AutoSize = true, AccessibleName = "Einfügen: " + spec.Name };
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
        Shown += (_, _) =>
        {
            approvalWatch.Restart(); approvalTimer.Start(); Inputs[0].Focus();
            // Covers a parent exit before a window handle was available for BeginInvoke.
            if (parentTermination.IsCancellationRequested) ParentEnded();
        };
        approvalTimer.Tick += (_, _) => CheckApprovalTimeout();
        FormClosing += (_, e) =>
        {
            if (running)
            {
                e.Cancel = true;
                closingAfterRun = true;
                lifecycle.Cancel();
                MessageLabel.Text = "Auftrag wird beendet und bereinigt …";
            }
            else ClearInputs();
        };
        RefreshValidity();
        parentRegistration = parentTermination.Register(() =>
        {
            // The linked token stops the native runner independently of this UI callback.
            try { if (IsHandleCreated) BeginInvoke(ParentEnded); }
            catch (InvalidOperationException) { /* Window already closing; no raw diagnostics. */ }
        });
    }

    private void ParentEnded()
    {
        if (IsDisposed) return;
        if (running)
        {
            closingAfterRun = true;
            MessageLabel.Text = "Aufrufer beendet. Auftrag wird beendet und bereinigt …";
        }
        else
        {
            // Preserve a completed result, especially CLEANUP_FAILED, while closing its display.
            if (!terminal) Result = new RunResult(Status.Cancelled);
            Close();
        }
    }

    internal void CheckApprovalTimeout()
    {
        if (!running && !terminal && approvalWatch.Elapsed >= approvalLimit)
            Finish(new RunResult(Status.TimedOut));
    }

    private void RefreshValidity()
    {
        if (running || terminal) return;
        StartButton.Enabled = Inputs.Count == request.Secrets.Length && Inputs.All(i => EnvironmentPolicy.ValidValue(i.Text));
        MessageLabel.Text = StartButton.Enabled ? "Auftrag prüfen und ausdrücklich starten." : "Alle benötigten Testwerte eingeben.";
    }

    private void ShowInputError(string text) => MessageLabel.Text = text;

    internal async Task StartApprovedAsync()
    {
        if (running || terminal) return;
        if (parentTermination.IsCancellationRequested) { ParentEnded(); return; }
        RefreshValidity();
        if (!StartButton.Enabled) return;
        // Timer messages can be delayed while another UI event is being processed.
        if (approvalWatch.Elapsed >= approvalLimit) { Finish(new RunResult(Status.TimedOut)); return; }
        running = true;
        approvalTimer.Stop();
        StartButton.Enabled = false;
        CancelButtonControl.Text = "Auftrag beenden";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < Inputs.Count; i++) values.Add(request.Secrets[i].Name, Inputs[i].Text);
        ClearInputs();
        MessageLabel.Text = "Auftrag läuft. Ausgaben werden verworfen.";
        Result = await lifecycle.StartAsync(request, values);
        running = false;
        // Disposal can finish the runner while this continuation is still queued in WinForms.
        if (IsDisposed || Disposing || lifecycleDisposed) return;
        Finish(Result);
        if (closingAfterRun || parentTermination.IsCancellationRequested) Close();
    }

    internal void CancelOrClose()
    {
        if (running)
        {
            lifecycle.Cancel();
            MessageLabel.Text = "Auftrag wird beendet und bereinigt …";
        }
        else Close();
    }

    private void Finish(RunResult result)
    {
        Result = result;
        terminal = true;
        approvalTimer.Stop();
        ClearInputs();
        StartButton.Enabled = false;
        CancelButtonControl.Text = "Schließen";
        MessageLabel.Text = StatusText.Category(result.Status) + " · " + StatusText.Describe(result.Status) +
            (result.TargetExitCode is uint target ? $" Ziel-Exitcode: {target}." : "") +
            (result.WindowsError is int error ? $" Windows-Fehlercode: {error}." : "");
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
            // Cancel and confirm native cleanup before destroying controls or reporting a UI failure.
            Result = lifecycle.StopAndWait(Result);
            lifecycleDisposed = true;
            running = false;
            terminal = true;
            ClearInputs();
            lifecycle.Dispose();
            parentRegistration.Dispose();
            approvalTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
