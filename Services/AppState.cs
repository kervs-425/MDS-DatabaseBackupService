namespace MDS.DatabaseBackupService.Services;

public sealed class AppState
{
    public DateTimeOffset? LastAttemptUtc { get; set; }

    public DateTimeOffset? LastSuccessfulBackupUtc { get; set; }

    public string? LastSuccessfulBackupFileName { get; set; }

    public DateTimeOffset? LastFailureUtc { get; set; }

    public string? LastFailureMessage { get; set; }
}
