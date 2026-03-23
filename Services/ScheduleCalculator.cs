namespace MDS.DatabaseBackupService.Services;

public static class ScheduleCalculator
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    public static DateTime GetNextAutomaticRun(BranchSettings settings, AppState state, DateTime nowLocal)
    {
        var baseDueTime = GetBaseDueTime(settings, state, nowLocal);
        if (baseDueTime > nowLocal)
        {
            return baseDueTime;
        }

        if (state.LastAttemptUtc is null)
        {
            return nowLocal;
        }

        var retryTime = state.LastAttemptUtc.Value.LocalDateTime.Add(RetryDelay);
        return retryTime > nowLocal ? retryTime : nowLocal;
    }

    private static DateTime GetBaseDueTime(BranchSettings settings, AppState state, DateTime nowLocal)
    {
        var scheduledTime = settings.GetScheduleTime().ToTimeSpan();
        if (state.LastSuccessfulBackupUtc is null)
        {
            return nowLocal.Date.Add(scheduledTime);
        }

        var lastSuccessLocal = state.LastSuccessfulBackupUtc.Value.LocalDateTime;
        return lastSuccessLocal.Date.AddDays(settings.ScheduleIntervalDays).Add(scheduledTime);
    }
}
