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
    public RequestException() : base("Auftrag ungültig. Format, lokale Pfade und Secret-Namen prüfen.") { }
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
        Status.Completed => "Ziel beendet; betreuter Prozessbaum bereinigt.",
        Status.Cancelled => "Auftrag abgebrochen; kein betreuter Prozess läuft weiter.",
        Status.TimedOut => "Zeitlimit erreicht; kein betreuter Prozess läuft weiter.",
        Status.Busy => "Ein anderer Auftrag ist bereits aktiv.",
        Status.InvalidRequest => "Auftrag ungültig. Format, lokale Pfade und Secret-Namen prüfen.",
        Status.StartFailed => "Ziel konnte nicht betreut gestartet werden.",
        Status.TargetFailed => "Ziel meldete einen Fehler; Prozessbaum bereinigt.",
        Status.CleanupFailed => "Beendigung konnte nicht vollständig bestätigt werden.",
        _ => "Interner Fehler; Auftrag beendet."
    };
}
