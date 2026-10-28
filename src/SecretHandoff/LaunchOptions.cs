using System.Globalization;
using SecretHandoff.Core;

namespace SecretHandoff;

internal sealed record LaunchOptions(string? RequestPath, uint? ParentId = null, long? ParentCreation = null)
{
    internal static LaunchOptions Parse(string[] args)
    {
        if (args.Length == 0) return new(RequestPath: null);
        if (args.Length == 2 && args[0] == "--request") return new(args[1]);
        if (args.Length == 5 && args[0] == "--request" && args[2] == "--watch-parent" &&
            uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint pid) && pid > 0 &&
            pid != Environment.ProcessId &&
            long.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out long creation) && creation > 0)
            return new(args[1], pid, creation);
        throw new RequestException();
    }
}
