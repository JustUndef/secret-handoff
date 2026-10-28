using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SecretHandoff.Core;

public static class EnvironmentPolicy
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH", "PATHEXT", "SystemRoot", "WINDIR", "COMSPEC", "TEMP", "TMP", "HOME", "USERPROFILE",
        "APPDATA", "LOCALAPPDATA", "PSModulePath", "ENV", "BASH_ENV", "JAVA_TOOL_OPTIONS",
        "_JAVA_OPTIONS", "JDK_JAVA_OPTIONS"
    };
    private static readonly string[] Prefixes = ["SHANDOFF_", "DOTNET_", "CORECLR_", "COMPlus_", "COR_",
        "NODE_", "PYTHON", "RUBY", "PERL", "LD_", "DYLD_", "ELECTRON_", "GIT_", "SSH_", "PROGRAM"];

    public static bool AllowedName(string name) => name.Length is > 0 and <= 128 &&
        Regex.IsMatch(name, "\\A[A-Za-z_][A-Za-z0-9_]*\\z", RegexOptions.CultureInvariant) &&
        !Reserved.Contains(name) && !Prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public static bool ValidValue(string value) => value.Length is > 0 and <= 16384 &&
        value.IndexOfAny(['\0', '\r', '\n']) < 0;

    internal static char[] Build(LaunchRequest request, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count != request.Secrets.Length || request.Secrets.Any(s =>
            !values.TryGetValue(s.Name, out var value) || !ValidValue(value)) ||
            values.Keys.Any(k => !request.Secrets.Any(s => string.Equals(k, s.Name, StringComparison.Ordinal))))
            throw new RequestException();
        var windows = new StringBuilder(32768);
        uint length = GetWindowsDirectoryW(windows, (uint)windows.Capacity);
        if (length == 0 || length >= windows.Capacity) throw new InvalidOperationException();
        string root = windows.ToString();
        var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = root, ["WINDIR"] = root,
            ["PATH"] = Path.Combine(root, "System32") + ";" + root
        };
        foreach (var secret in request.Secrets) entries.Add(secret.Name, values[secret.Name]);
        // Never concatenate a second managed string containing all secret values.
        var block = new char[entries.Sum(p => p.Key.Length + p.Value.Length + 2) + 1];
        int offset = 0;
        foreach (var pair in entries)
        {
            pair.Key.CopyTo(0, block, offset, pair.Key.Length); offset += pair.Key.Length;
            block[offset++] = '=';
            pair.Value.CopyTo(0, block, offset, pair.Value.Length); offset += pair.Value.Length;
            block[offset++] = '\0';
        }
        return block;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetWindowsDirectoryW(StringBuilder buffer, uint size);
}
