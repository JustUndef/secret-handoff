using System.Text.Json;
using SecretHandoff.Core;

internal static partial class Program
{
    private static void RegisterLocalPathTests()
    {
        Test("local-path-native-valid", LocalPathNativeValid);
        Test("local-path-reject-drive-before-attributes", LocalPathRejectedDrives);
        Test("local-path-syntax-before-probe", LocalPathSyntax);
        Test("local-path-components-and-types", LocalPathComponents);
        Test("local-path-loader-checks-all-paths", LocalPathLoader);
        Test("local-path-post-preview-revalidation", LocalPathPostPreview);
        Test("local-path-immediate-prestart-revalidation", LocalPathBeforeCreate);
    }

    private static void LocalPathNativeValid()
    {
        Assert(LocalPathPolicy.Native.File(probe) == Path.GetFullPath(probe));
        Assert(LocalPathPolicy.Native.Directory(scratch + "\\") == scratch);
        var loaded = RequestLoader.Load(WriteRequest(Json()));
        Assert(loaded.Executable == Path.GetFullPath(probe) && loaded.WorkingDirectory == Path.GetFullPath(Path.GetDirectoryName(probe)!));
    }

    private static void LocalPathRejectedDrives()
    {
        // No real mapping, share, remote file or unavailable drive is created/accessed.
        foreach (var type in new[] { DriveType.Network, DriveType.Unknown, DriveType.NoRootDirectory, (DriveType)123 })
        {
            var probe = new PathMetadataProbe { Drive = _ => type };
            var policy = new LocalPathPolicy(probe);
            PathRejected(() => policy.File(@"Z:\synthetic\worker.exe"));
            Assert(probe.DriveChecks == 1 && probe.AttributesVisited.Count == 0, "DRIVE_ORDER");
            PathRejected(() => RequestLoader.Load(@"Z:\synthetic\request.json", policy));
            Assert(probe.DriveChecks == 2 && probe.AttributesVisited.Count == 0, "REQUEST_DRIVE_ORDER");
        }
    }

    private static void LocalPathSyntax()
    {
        var probe = new PathMetadataProbe();
        var policy = new LocalPathPolicy(probe);
        foreach (string path in new[]
        {
            "", "relative.exe", @"C:worker.exe", @"\worker.exe", @"\\server\share\worker.exe",
            @"\\?\C:\worker.exe", @"C:\x \worker.exe", @"C:\x.\worker.exe", @"C:\worker.exe:stream",
            @"C:\NUL.exe", @"C:\COM1.exe", @"C:\COM1 .exe", @"C:\LPT¹.exe", @"C:\CONIN$", "C:\\x\u202eworker.exe",
            @"C:\*.exe", @"C:\x?\worker.exe", @"C:\worker.exe\"
        }) PathRejected(() => policy.File(path));
        Assert(probe.DriveChecks == 0 && probe.AttributesVisited.Count == 0, "SYNTAX_ORDER");
        Assert(policy.File(@"c:\synthetic\worker.exe") == @"C:\synthetic\worker.exe");
        Assert(policy.File("c:/synthetic/worker.exe") == @"C:\synthetic\worker.exe");
        Assert(policy.File(@"C:\x\..\synthetic\.\worker.exe") == @"C:\synthetic\worker.exe");
        Assert(policy.File(@"C:\synthetic\\worker.exe") == @"C:\synthetic\worker.exe");
        Assert(policy.Directory(@"c:\synthetic\") == @"C:\synthetic");
        Assert(policy.Directory(@"c:\") == @"C:\");
    }

    private static void LocalPathComponents()
    {
        foreach (string blocked in new[] { @"C:\", @"C:\synthetic", @"C:\synthetic\worker.exe" })
        {
            var probe = new PathMetadataProbe
            {
                Attributes = path => path == blocked
                    ? PathMetadataProbe.DefaultAttributes(path) | FileAttributes.ReparsePoint
                    : PathMetadataProbe.DefaultAttributes(path)
            };
            PathRejected(() => new LocalPathPolicy(probe).File(@"C:\synthetic\worker.exe"));
            Assert(probe.AttributesVisited[^1] == blocked, "FOLLOWED_REPARSE");
        }
        foreach (FileAttributes? attributes in new FileAttributes?[] { null, FileAttributes.Device, FileAttributes.Directory })
        {
            var probe = new PathMetadataProbe
            {
                Attributes = path => path == @"C:\synthetic\worker.exe" ? attributes : FileAttributes.Directory
            };
            PathRejected(() => new LocalPathPolicy(probe).File(@"C:\synthetic\worker.exe"));
        }
        var fileParent = new PathMetadataProbe { Attributes = _ => FileAttributes.Normal };
        PathRejected(() => new LocalPathPolicy(fileParent).Directory(@"C:\synthetic"));
        Assert(fileParent.AttributesVisited.Count == 1, "TRAVERSED_FILE_PARENT");
        foreach (var type in new[] { DriveType.Fixed, DriveType.Removable, DriveType.CDRom, DriveType.Ram })
            Assert(new LocalPathPolicy(new PathMetadataProbe { Drive = _ => type }).Directory(@"C:\synthetic") == @"C:\synthetic");
    }

    private static void LocalPathLoader()
    {
        string source = WriteRequest(Json());
        var sourceReparse = new PathMetadataProbe
        {
            Attributes = path => path == source ? FileAttributes.ReparsePoint : PathMetadataProbe.DefaultAttributes(path)
        };
        PathRejected(() => RequestLoader.Load(source, new LocalPathPolicy(sourceReparse)));
        Assert(sourceReparse.AttributesVisited[^1] == source, "REQUEST_REPARSE");

        foreach (bool remoteExecutable in new[] { true, false })
        {
            string request = WriteRequest(JsonSerializer.Serialize(new
            {
                executable = remoteExecutable ? @"Z:\synthetic\worker.exe" : probe,
                arguments = new[] { "fail" },
                workingDirectory = remoteExecutable ? Path.GetDirectoryName(probe) : @"Z:\synthetic",
                secrets = new[] { new { name = "EXAMPLE_API_TOKEN", description = "Synthetischer Testwert" } }
            }));
            var metadata = new PathMetadataProbe { Drive = path => path == @"Z:\" ? DriveType.Network : DriveType.Fixed };
            PathRejected(() => RequestLoader.Load(request, new LocalPathPolicy(metadata)));
            Assert(!metadata.AttributesVisited.Any(path => path.StartsWith(@"Z:\", StringComparison.Ordinal)), "REMOTE_ATTRIBUTES");
        }
    }

    private static void LocalPathPostPreview()
    {
        string source = WriteRequest(Json());
        var metadata = new PathMetadataProbe();
        var policy = new LocalPathPolicy(metadata);
        var snapshot = RequestLoader.Load(source, policy);
        // The source content remains an immutable snapshot; only target paths are rechecked.
        File.WriteAllText(source, "{}");
        metadata.Drive = _ => DriveType.Network;
        metadata.AttributesVisited.Clear();
        bool created = false;
        var values = Values();
        var runner = new WindowsProcessRunner(TimeSpan.FromSeconds(2), policy) { CreatedForTest = _ => created = true };
        AssertResult(runner.RunAsync(snapshot, values, CancellationToken.None).GetAwaiter().GetResult(), Status.InvalidRequest);
        Assert(!created && values.Count == 0 && metadata.AttributesVisited.Count == 0, "POST_PREVIEW_START");
    }

    private static void LocalPathBeforeCreate()
    {
        foreach (bool changeDrive in new[] { true, false })
        {
            int targetChecks = 0;
            string targetRoot = Path.GetPathRoot(probe)!;
            var metadata = new PathMetadataProbe();
            metadata.Drive = path => changeDrive && path == targetRoot && ++targetChecks >= 3
                ? DriveType.Network : DriveType.Fixed;
            int executableChecks = 0;
            metadata.Attributes = path => !changeDrive && path == Path.GetFullPath(probe) && ++executableChecks >= 2
                ? FileAttributes.ReparsePoint : PathMetadataProbe.DefaultAttributes(path);
            bool created = false;
            string marker = Path.Combine(scratch, Guid.NewGuid().ToString("N") + ".prestart-marker");
            var values = Values();
            var runner = new WindowsProcessRunner(TimeSpan.FromSeconds(2), new LocalPathPolicy(metadata))
            { CreatedForTest = _ => created = true };
            AssertResult(runner.RunAsync(Request("tree", marker, "wait"), values, CancellationToken.None).GetAwaiter().GetResult(), Status.InvalidRequest);
            Assert(!created && !File.Exists(marker) && values.Count == 0, "PRECREATE_START");
            Assert(changeDrive ? targetChecks == 3 : executableChecks == 2, "PRECREATE_CHECK_MISSING");
        }
    }

    private static void PathRejected(Action action)
    {
        try { action(); }
        catch (RequestException) { return; }
        throw new TestFailure("LOCAL_PATH_ACCEPTED");
    }

    private sealed class PathMetadataProbe : ILocalPathProbe
    {
        internal Func<string, DriveType> Drive { get; set; } = _ => DriveType.Fixed;
        internal Func<string, FileAttributes?> Attributes { get; set; } = DefaultAttributes;
        internal int DriveChecks { get; private set; }
        internal List<string> AttributesVisited { get; } = [];
        public DriveType GetDriveType(string root) { DriveChecks++; return Drive(root); }
        public FileAttributes? GetAttributes(string path) { AttributesVisited.Add(path); return Attributes(path); }
        internal static FileAttributes? DefaultAttributes(string path) =>
            path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Normal : FileAttributes.Directory;
    }
}
