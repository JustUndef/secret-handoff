using System.Globalization;
using SecretHandoff.Core;

namespace SecretHandoff;

internal sealed class CliException(string diagnostic) : Exception(diagnostic);

internal sealed record LaunchOptions(string? RequestPath, uint? ParentId = null, long? ParentCreation = null,
    LaunchRequest? DirectRequest = null, bool Help = false)
{
    internal static LaunchOptions Parse(string[] args) => Parse(args, Environment.CurrentDirectory);

    internal static LaunchOptions Parse(string[] args, string callerDirectory)
    {
        if (args.Length == 0) return new(RequestPath: null);
        if (args.Length == 1 && args[0] == "--help") return new(null, Help: true);
        string? path = null, directory = null;
        uint? parent = null;
        long? creation = null;
        var secrets = new List<SecretSpec>();
        int target = -1;
        bool mayDescribe = false;
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option == "--") { target = i + 1; break; }
            string Value()
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new CliException("Missing option value. Help: --help.");
                return args[i];
            }
            bool descriptionAllowed = mayDescribe;
            mayDescribe = false;
            switch (option)
            {
                case "--request":
                    if (path != null) throw new CliException("--request may appear only once.");
                    path = Value();
                    if (path.Length == 0) throw new CliException("Missing JSON path. Help: --help.");
                    break;
                case "--working-directory":
                    if (directory != null) throw new CliException("--working-directory may appear only once.");
                    directory = Value();
                    if (directory.Length == 0) throw new CliException("Missing working directory. Help: --help.");
                    break;
                case "--secret":
                    secrets.Add(new SecretSpec(Value(), ""));
                    mayDescribe = true;
                    break;
                case "--description":
                    if (!descriptionAllowed) throw new CliException("--description must immediately follow --secret NAME.");
                    secrets[^1] = secrets[^1] with { Description = Value() };
                    break;
                case "--watch-parent":
                    if (parent != null) throw new CliException("--watch-parent may appear only once.");
                    if (!uint.TryParse(Value(), NumberStyles.None, CultureInfo.InvariantCulture, out uint pid) ||
                        pid == 0 || pid == Environment.ProcessId ||
                        !long.TryParse(Value(), NumberStyles.None, CultureInfo.InvariantCulture, out long time) || time <= 0)
                        throw new CliException("Invalid caller binding. Expected: --watch-parent PID FILETIME.");
                    parent = pid; creation = time;
                    break;
                default:
                    throw new CliException("Unknown option or unexpected argument. --help must be used alone.");
            }
        }
        if (path != null)
        {
            if (target != -1 || directory != null || secrets.Count != 0)
                throw new CliException("--request cannot be combined with direct-mode options or --.");
            return new(path, parent, creation);
        }
        if (target < 0 || target >= args.Length || secrets.Count == 0)
            throw new CliException("Direct mode requires --secret NAME and -- PROGRAM [ARGUMENTS]. Help: --help.");
        string resolved;
        try
        {
            // No mutable current-directory lookup after this point; reject drive-relative paths.
            if (directory != null && Path.IsPathRooted(directory) && !Path.IsPathFullyQualified(directory))
                throw new RequestException();
            resolved = directory == null ? callerDirectory : Path.IsPathFullyQualified(directory)
                ? directory : Path.Combine(callerDirectory, directory);
        }
        catch { throw new RequestException(); }
        return new(null, parent, creation,
            RequestLoader.Create(args[target], args.Skip(target + 1), resolved, secrets));
    }
}
