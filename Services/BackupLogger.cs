namespace MDS.DatabaseBackupService.Services;

public sealed class BackupLogger
{
    private readonly string _logFilePath;
    private readonly object _sync = new();

    public BackupLogger(BranchSettings settings)
    {
        _logFilePath = settings.LogFilePath;
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_sync)
        {
            File.AppendAllText(_logFilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        }
    }
}
