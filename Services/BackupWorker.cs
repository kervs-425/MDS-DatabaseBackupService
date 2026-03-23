namespace MDS.DatabaseBackupService.Services;

public sealed class BackupWorker : BackgroundService
{
    private readonly BackupService _backupService;
    private readonly BranchSettings _settings;
    private readonly BackupLogger _logger;
    private readonly StateStore _stateStore;

    private AppState _state;

    public BackupWorker(BackupService backupService, BranchSettings settings, BackupLogger logger)
    {
        _backupService = backupService;
        _settings = settings;
        _logger = logger;
        _stateStore = new StateStore(settings.StateFilePath);
        _state = _stateStore.Load();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Info("Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsBackupDue())
                {
                    await RunAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _state.LastFailureUtc = DateTimeOffset.UtcNow;
                _state.LastFailureMessage = ex.Message;
                _stateStore.Save(_state);
                _logger.Error($"Task failed: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.Info("Service stopped.");
    }

    private bool IsBackupDue()
    {
        var nextRun = ScheduleCalculator.GetNextAutomaticRun(_settings, _state, DateTime.Now);
        return nextRun <= DateTime.Now;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Scheduled task started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        _state.LastAttemptUtc = DateTimeOffset.UtcNow;
        _stateStore.Save(_state);

        var result = await _backupService.RunBackupAsync(cancellationToken);

        _state.LastSuccessfulBackupUtc = DateTimeOffset.UtcNow;
        _state.LastSuccessfulBackupFileName = Path.GetFileName(result.LocalBackupPath);
        _state.LastFailureUtc = null;
        _state.LastFailureMessage = null;
        _stateStore.Save(_state);

        _logger.Info($"Task completed. Output: {result.LocalBackupPath}");
    }
}
