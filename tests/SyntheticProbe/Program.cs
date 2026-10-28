using System.Diagnostics;

// Synthetic fixtures only. Never print environment contents or arguments.
internal static class Program
{
    internal static readonly string[] Vector = ["", "a b", "quote\"inside", "trailing\\", "\\\"", "Ünicode", "& | > % $ ;", "%PATH%", "${NAME}"];

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return 40;
            if (args[0] == "demo")
                return Environment.GetEnvironmentVariable("EXAMPLE_API_TOKEN") == "synthetic-demo-one" &&
                    Environment.GetEnvironmentVariable("SECOND_SECRET") == "synthetic-demo-two" ? 0 : 49;
            if (args[0] == "check")
            {
                if (Environment.GetEnvironmentVariable("EXAMPLE_API_TOKEN") != " synthetic-only-Ü\\\" ") return 41;
                if (Environment.GetEnvironmentVariable("SECOND_SECRET") != "synthetic-two") return 42;
                if (!args.Skip(1).SequenceEqual(Vector)) return 43;
                var keys = Environment.GetEnvironmentVariables().Keys.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!keys.SetEquals(["PATH", "SystemRoot", "WINDIR", "EXAMPLE_API_TOKEN", "SECOND_SECRET"])) return 44;
                if (Environment.CurrentDirectory != AppContext.BaseDirectory.TrimEnd('\\')) return 45;
                return 0;
            }
            if (args[0] == "fail") return 37;
            if (args[0] == "flood")
            {
                Console.Out.Write(Environment.GetEnvironmentVariable("EXAMPLE_API_TOKEN"));
                Console.Error.Write(Environment.GetEnvironmentVariable("SECOND_SECRET"));
                string synthetic = new('S', 8192);
                for (int i = 0; i < 2048; i++) { Console.Out.Write(synthetic); Console.Error.Write(synthetic); }
                Console.OpenStandardOutput().Write([0, 255, 0, 254]);
                return 0;
            }
            if (args[0] == "leaf")
            {
                File.WriteAllText(args[1], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Thread.Sleep(Timeout.Infinite);
            }
            if (args[0] == "tree")
            {
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("leaf"); start.ArgumentList.Add(args[1] + ".child");
                using var child = Process.Start(start)!;
                var watch = Stopwatch.StartNew();
                while (!File.Exists(args[1] + ".child") && watch.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                if (!File.Exists(args[1] + ".child")) return 46;
                File.WriteAllText(args[1], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (args[2] == "exit") return 0;
                Thread.Sleep(Timeout.Infinite);
            }
            return 47;
        }
        catch { return 48; }
    }
}
