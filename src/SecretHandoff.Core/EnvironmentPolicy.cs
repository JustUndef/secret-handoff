using System.Text.RegularExpressions;

namespace SecretHandoff.Core;

public static class EnvironmentPolicy
{
    // Runtime configuration only. Arbitrary ambient credentials are never copied.
    internal static readonly string[] RuntimeNames = ["SystemRoot", "WINDIR", "PATH", "PATHEXT", "COMSPEC",
        "TEMP", "TMP", "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA",
        "PROGRAMDATA", "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432",
        "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS"];
    private static readonly HashSet<string> Reserved = new(RuntimeNames.Concat(new[]
    {
        "HOME", "PSModulePath", "ENV", "BASH_ENV", "NODE_OPTIONS", "PYTHONPATH", "PYTHONHOME",
        "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "JDK_JAVA_OPTIONS", "LD_PRELOAD", "LD_LIBRARY_PATH",
        "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE"
    }), StringComparer.OrdinalIgnoreCase);

    public static bool AllowedName(string name) => name.Length is > 0 and <= 128 &&
        Regex.IsMatch(name, "\\A[A-Za-z_][A-Za-z0-9_]*\\z", RegexOptions.CultureInvariant) && !Reserved.Contains(name);

    public static bool ValidValue(string value) => value.Length is > 0 and <= 16384 && value.IndexOfAny(['\0', '\r', '\n']) < 0;

    internal static char[] Build(LaunchRequest request, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count != request.Secrets.Length || request.Secrets.Any(s =>
            !values.TryGetValue(s.Name, out var value) || !ValidValue(value)) ||
            values.Keys.Any(k => !request.Secrets.Any(s => s.Name == k))) throw new RequestException();
        var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in RuntimeNames)
            if (Environment.GetEnvironmentVariable(name) is string value) entries[name] = value;
        foreach (var entry in values) entries.Add(entry.Key, entry.Value);
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
}
