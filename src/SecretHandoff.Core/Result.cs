using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("SecretHandoff.Tests")]

namespace SecretHandoff.Core;

public enum Status
{
    Completed = 0, Cancelled = 2, InvalidRequest = 5,
    StartFailed = 6, TargetFailed = 7, InternalError = 8, StopFailed = 9
}
public sealed record RunResult(Status Status, uint? TargetExitCode = null, int? WindowsError = null);
public sealed class RequestException : Exception
{
    public RequestException() : base("Invalid request. Check paths, arguments, and secret names.") { }
}
public static class StatusText
{
    public static string Category(Status status) => status switch
    {
        Status.Completed => "COMPLETED", Status.Cancelled => "CANCELLED",
        Status.InvalidRequest => "INVALID_REQUEST", Status.StartFailed => "START_FAILED",
        Status.TargetFailed => "TARGET_FAILED", Status.StopFailed => "STOP_FAILED", _ => "INTERNAL_ERROR"
    };
    public static string Describe(Status status) => status switch
    {
        Status.Completed => "Target exited successfully.",
        Status.Cancelled => "Request cancelled; the direct target was stopped if started.",
        Status.InvalidRequest => "Invalid request. Check paths, arguments, and secret names.",
        Status.StartFailed => "Target could not be started.",
        Status.TargetFailed => "Target reported a failure.",
        Status.StopFailed => "Direct target termination could not be confirmed.",
        _ => "Internal error; request ended."
    };
}
