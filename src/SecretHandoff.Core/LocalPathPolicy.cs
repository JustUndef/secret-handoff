using System.Runtime.InteropServices;

namespace SecretHandoff.Core;

// Per-instance metadata probe: tests can model drive/attribute changes without mapping a drive
// or contacting a network. There is no public switch that disables the production policy.
internal interface ILocalPathProbe
{
    DriveType GetDriveType(string root);
    FileAttributes? GetAttributes(string path);
}

internal sealed class LocalPathPolicy(ILocalPathProbe probe)
{
    internal static LocalPathPolicy Native { get; } = new(new NativeLocalPathProbe());

    internal string File(string path) => Validate(path, directory: false);
    internal string Directory(string path) => Validate(path, directory: true);

    private string Validate(string path, bool directory)
    {
        try
        {
            string normalized = Normalize(path, directory);
            string root = normalized[..3];
            // Must precede every attribute/content access, including the root directory.
            if (probe.GetDriveType(root) is not (DriveType.Fixed or DriveType.Removable or DriveType.CDRom or DriveType.Ram))
                throw new RequestException();

            CheckAttributes(root, directory: true);
            string current = root;
            string[] components = normalized.Length == 3 ? [] : normalized[3..].Split('\\');
            for (int i = 0; i < components.Length; i++)
            {
                current = Path.Combine(current, components[i]);
                // Inspect each component itself; never intentionally traverse an accepted link.
                // Another process can still replace a previously inspected component (TOCTOU).
                CheckAttributes(current, directory: i < components.Length - 1 || directory);
            }
            if (!directory && components.Length == 0) throw new RequestException();
            return normalized;
        }
        catch (Exception e) when (e is not OutOfMemoryException && e is not StackOverflowException)
        {
            throw new RequestException();
        }
    }

    private void CheckAttributes(string path, bool directory)
    {
        var attributes = probe.GetAttributes(path) ?? throw new RequestException();
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            attributes.HasFlag(FileAttributes.Directory) != directory)
            throw new RequestException();
    }

    private static string Normalize(string path, bool directory)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 3 || !char.IsAsciiLetter(path[0]) ||
            path[1] != ':' || path[2] is not ('\\' or '/') || RequestLoader.UnsafeDisplay(path) ||
            path.IndexOfAny(['"', '<', '>', '|', '?', '*']) >= 0 || path[2..].Contains(':'))
            throw new RequestException();
        // Canonicalize ordinary DOS paths syntactically before touching the filesystem. The
        // loader stores this same canonical spelling in the approved metadata snapshot.
        string candidate = path.Replace('/', '\\');
        if (!directory && candidate.Length > 3 && candidate.EndsWith('\\')) throw new RequestException();
        if (candidate.Length > 3)
            foreach (string component in candidate[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                if (component is "." or "..") continue;
                if (component.EndsWith('.') ||
                    component.EndsWith(' ') || IsDeviceName(component)) throw new RequestException();
            }
        string full = Path.GetFullPath(candidate);
        if (full.Length > 3 && full.EndsWith('\\')) full = full[..^1];
        return char.ToUpperInvariant(full[0]) + full[1..];
    }

    private static bool IsDeviceName(string component)
    {
        string name = component.Split('.')[0].TrimEnd(' ');
        if (name.Equals("CON", StringComparison.OrdinalIgnoreCase) || name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("AUX", StringComparison.OrdinalIgnoreCase) || name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || name.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase)) return true;
        return name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(name[3]);
    }

    private sealed class NativeLocalPathProbe : ILocalPathProbe
    {
        public DriveType GetDriveType(string root)
        {
            var type = (DriveType)GetDriveTypeW(root);
            if (type is not (DriveType.Fixed or DriveType.Removable or DriveType.CDRom or DriveType.Ram)) return type;
            // SUBST/DOS aliases can hide unchecked parent components. Only direct device mappings
            // are supported; querying the DOS namespace does not open the mapped target.
            var target = new char[32768];
            if (QueryDosDeviceW(root[..2], target, target.Length) == 0) return DriveType.Unknown;
            int end = Array.IndexOf(target, '\0');
            if (end <= 0 || !new string(target, 0, end).StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
                return DriveType.Unknown;
            return type;
        }

        public FileAttributes? GetAttributes(string path)
        {
            // GetFileAttributesW returns the final link's own attributes, not its target's.
            uint attributes = GetFileAttributesW(path);
            return attributes == uint.MaxValue ? null : (FileAttributes)attributes;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetDriveTypeW(string root);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint QueryDosDeviceW(string name, [Out] char[] target, int maxLength);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetFileAttributesW(string path);
    }
}
