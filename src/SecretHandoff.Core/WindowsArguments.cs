using System.Text;

namespace SecretHandoff.Core;

public static class WindowsArguments
{
    public static string Encode(string executable, IEnumerable<string> arguments) =>
        "\"" + executable + "\"" + string.Concat(arguments.Select(a => " " + Quote(a)));

    private static string Quote(string value)
    {
        var output = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            output.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            output.Append(c);
            slashes = 0;
        }
        output.Append('\\', slashes * 2);
        return output.Append('"').ToString();
    }
}
