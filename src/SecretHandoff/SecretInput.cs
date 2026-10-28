using SecretHandoff.Core;

namespace SecretHandoff;

internal sealed class SecretInput : TextBox
{
    private readonly Func<string?> readClipboard;
    private readonly Action<string> showError;

    internal SecretInput(Func<string?> readClipboard, Action<string> showError)
    {
        this.readClipboard = readClipboard;
        this.showError = showError;
        UseSystemPasswordChar = true;
        ShortcutsEnabled = false;
        MaxLength = 16385; // Detect overflow rather than silently accepting a truncated maximum value.
        ContextMenuStrip = new ContextMenuStrip();
        ContextMenuStrip.Items.Add("Einfügen", null, (_, _) => PasteValue());
    }

    internal void PasteValue()
    {
        try
        {
            string? text = readClipboard();
            if (text is null) { showError("Die Zwischenablage enthält keinen Text."); return; }
            int length = TextLength - SelectionLength + text.Length;
            if (length > 16384 || text.IndexOfAny(['\0', '\r', '\n']) >= 0)
            { showError("Nur einzeilige Werte mit höchstens 16.384 Zeichen einfügen."); return; }
            SelectedText = text;
            Focus();
        }
        catch { showError("Zwischenablage momentan nicht verfügbar. Bitte erneut versuchen."); }
    }

    internal bool HandleSecretKey(Keys keys)
    {
        if (keys == (Keys.Control | Keys.V) || keys == (Keys.Shift | Keys.Insert)) { PasteValue(); return true; }
        if (keys == (Keys.Control | Keys.A)) { SelectAll(); return true; }
        return keys == (Keys.Control | Keys.C) || keys == (Keys.Control | Keys.X) ||
            keys == (Keys.Control | Keys.Insert) || keys == (Keys.Shift | Keys.Delete) || keys == Keys.Enter;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        HandleSecretKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x302) { PasteValue(); return; } // WM_PASTE uses the same validated path.
        if (m.Msg is 0x300 or 0x301 or 0x303) return; // WM_CUT / WM_COPY / WM_CLEAR
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ContextMenuStrip?.Dispose();
        base.Dispose(disposing);
    }
}
