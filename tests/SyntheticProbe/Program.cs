using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

internal static class Program
{
    internal static readonly string[] Vector = ["", "a b", "quote\"inside", "trailing\\", "\\\"", "Unicode-\u03A9", "& | > % $ ;", "%PATH%", "${NAME}", "--help", "--watch-caller", "--"];
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return 40;
            switch (args[0])
            {
                case "quiet": return 0;
                case "handle-check":
                    return WaitForSingleObject(new IntPtr(long.Parse(args[1], CultureInfo.InvariantCulture)), 0) == uint.MaxValue &&
                        Marshal.GetLastWin32Error() == 6 && Console.OpenStandardInput().ReadByte() == -1 ? 0 : 44;
                case "demo":
                case "exchange":
                    if (!ValidInput()) return 49;
                    Console.WriteLine("synthetic-result-credential");
                    Console.Error.WriteLine("synthetic-diagnostic");
                    return 0;
                case "roundtrip":
                    if (Environment.GetEnvironmentVariable("EXAMPLE_API_TOKEN") != " synthetic-only-\u03A9\\\" " ||
                        Environment.GetEnvironmentVariable("SECOND_SECRET") != "synthetic-two" ||
                        !args.Skip(2).SequenceEqual(Vector) || Environment.CurrentDirectory != args[1]) return 41;
                    if (Environment.GetEnvironmentVariable("SHANDOFF_FOREIGN_TOKEN") != null ||
                        Environment.GetEnvironmentVariable("NODE_OPTIONS") != null) return 42;
                    if (Environment.GetEnvironmentVariable("TEMP") == null || Environment.GetEnvironmentVariable("USERPROFILE") == null) return 43;
                    Console.WriteLine("ROUNDTRIP_OK"); return 0;
                case "fail":
                    Console.Out.Write("partial-result"); Console.Error.Write("synthetic-failure"); return 37;
                case "flood":
                    byte[] bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
                    var stdout = Task.Run(() => { using var s = Console.OpenStandardOutput(); for (int i = 0; i < 16384; i++) s.Write(bytes); });
                    var stderr = Task.Run(() => { using var s = Console.OpenStandardError(); for (int i = 0; i < 16384; i++) s.Write(bytes); });
                    Task.WaitAll(stdout, stderr); return 0;
                case "wait":
                    Publish(args[1], Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(Timeout.Infinite); return 0;
                case "child":
                    Thread.Sleep(Timeout.Infinite); return 0;
                case "tree":
                    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                    start.ArgumentList.Add("child");
                    using (var child = Process.Start(start)!)
                    {
                        Publish(args[1], child.Id.ToString(CultureInfo.InvariantCulture) + " " + child.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture));
                        while (!File.Exists(args[1] + ".release")) Thread.Sleep(20);
                    }
                    return 0;
                default: return 47;
            }
        }
        catch { return 48; }
    }
    private static bool ValidInput() => Environment.GetEnvironmentVariable("EXAMPLE_API_TOKEN") == "synthetic-demo-one" &&
        Environment.GetEnvironmentVariable("SECOND_SECRET") == "synthetic-demo-two";
    private static void Publish(string path, string value)
    {
        File.WriteAllText(path + ".pending", value);
        File.Move(path + ".pending", path);
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
}
