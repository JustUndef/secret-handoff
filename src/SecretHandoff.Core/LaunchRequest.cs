using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace SecretHandoff.Core;

public sealed record SecretSpec(string Name, string Description);

// Immutable metadata only. Secret values never belong in this model.
public sealed record LaunchRequest(string SourcePath, string Executable,
    ImmutableArray<string> Arguments, string WorkingDirectory, ImmutableArray<SecretSpec> Secrets)
{
    public void Revalidate() => Revalidate(LocalPathPolicy.Native);

    internal void Revalidate(LocalPathPolicy paths)
    {
        paths.File(Executable);
        paths.Directory(WorkingDirectory);
        if (!Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            Arguments.Length > 128 || Arguments.Any(a => a.Contains('\0')) ||
            WindowsArguments.Encode(Executable, Arguments).Length >= 32767 ||
            Secrets.Length is < 1 or > 16 ||
            Secrets.Any(s => !EnvironmentPolicy.AllowedName(s.Name)) ||
            Secrets.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Secrets.Length)
            throw new RequestException();
    }
}

public static class RequestLoader
{
    public static LaunchRequest Load(string path) => Load(path, LocalPathPolicy.Native);

    internal static LaunchRequest Load(string path, LocalPathPolicy paths)
    {
        try
        {
            path = paths.File(path);
            // Bounded read even if another process grows/replaces the file.
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytes = new byte[65537];
            int count = 0, read;
            while (count < bytes.Length && (read = file.Read(bytes, count, bytes.Length - count)) > 0) count += read;
            if (count > 65536) throw new RequestException();
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            ExactKeys(root, "executable", "arguments", "workingDirectory", "secrets");
            var executable = paths.File(Text(root.GetProperty("executable")));
            var directory = paths.Directory(Text(root.GetProperty("workingDirectory")));
            var args = root.GetProperty("arguments").EnumerateArray().Select(Text).ToImmutableArray();
            var secrets = root.GetProperty("secrets").EnumerateArray().Select(s =>
            {
                ExactKeys(s, "name", "description");
                var name = Text(s.GetProperty("name"));
                var description = Text(s.GetProperty("description"));
                if (description.Length > 512 || UnsafeDisplay(description)) throw new RequestException();
                return new SecretSpec(name, description);
            }).ToImmutableArray();
            var request = new LaunchRequest(path, executable, args, directory, secrets);
            request.Revalidate(paths);
            return request;
        }
        catch (Exception e) when (e is not OutOfMemoryException && e is not StackOverflowException)
        {
            // Do not attach the source exception: paths/JSON may contain private input.
            throw new RequestException();
        }
    }

    private static string Text(JsonElement element) => element.GetString() ?? throw new RequestException();

    private static void ExactKeys(JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new RequestException();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!keys.Contains(property.Name) || !found.Add(property.Name)) throw new RequestException();
        if (found.Count != keys.Length) throw new RequestException();
    }

    internal static bool UnsafeDisplay(string value) => value.Any(c =>
        char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format);

}
