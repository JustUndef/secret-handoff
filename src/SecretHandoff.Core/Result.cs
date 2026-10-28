using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SecretHandoff.Tests")]

namespace SecretHandoff.Core;

public enum Status
{
    Completed = 0, Cancelled = 2, TimedOut = 3, Busy = 4, InvalidRequest = 5,
    StartFailed = 6, TargetFailed = 7, InternalError = 8, CleanupFailed = 9
}

public sealed record RunResult(Status Status, uint? TargetExitCode = null, int? WindowsError = null);

public sealed class RequestException : Exception
{
    public RequestException() : base("Invalid request. Check the format, local paths, and secret names.") { }
}

public static class StatusText
{
    public static string Category(Status status) => status switch
    {
        Status.Completed => "COMPLETED", Status.Cancelled => "CANCELLED",
        Status.TimedOut => "TIMED_OUT", Status.Busy => "BUSY",
        Status.InvalidRequest => "INVALID_REQUEST", Status.StartFailed => "START_FAILED",
        Status.TargetFailed => "TARGET_FAILED", Status.CleanupFailed => "CLEANUP_FAILED",
        _ => "INTERNAL_ERROR"
    };

    public static string Describe(Status status) => status switch
    {
        Status.Completed => "Target exited; managed process tree cleaned up.",
        Status.Cancelled => "Request cancelled; no managed process remains running.",
        Status.TimedOut => "Time limit reached; no managed process remains running.",
        Status.Busy => "Another request is already active.",
        Status.InvalidRequest => "Invalid request. Check the format, local paths, and secret names.",
        Status.StartFailed => "Target could not be started under supervision.",
        Status.TargetFailed => "Target reported a failure; process tree cleaned up.",
        Status.CleanupFailed => "Process termination could not be fully confirmed.",
        _ => "Internal error; request ended."
    };
}
