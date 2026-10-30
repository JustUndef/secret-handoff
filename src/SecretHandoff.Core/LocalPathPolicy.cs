namespace SecretHandoff.Core;

// Filesystem spelling/existence validation, not a code identity or isolation boundary.
internal sealed class LocalPathPolicy
{
    internal static LocalPathPolicy Native { get; } = new();
    internal string File(string path) => Validate(path, false);
    internal string Directory(string path) => Validate(path, true);

    private static string Validate(string path, bool directory)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || RequestLoader.UnsafeDisplay(path) ||
                path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                path.IndexOfAny(['"', '<', '>', '|', '?', '*']) >= 0) throw new RequestException();
            string full = Path.GetFullPath(path);
            // No alternate data stream or ambiguous trailing dot/space spelling.
            string tail = full[Path.GetPathRoot(full)!.Length..];
            if (tail.Contains(':') || tail.Split('\\', '/').Any(p => p.EndsWith(' ') || p.EndsWith('.')))
                throw new RequestException();
            if (directory ? !System.IO.Directory.Exists(full) : !System.IO.File.Exists(full)) throw new RequestException();
            return Path.TrimEndingDirectorySeparator(full);
        }
        catch { throw new RequestException(); }
    }
}
