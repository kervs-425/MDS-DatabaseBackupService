using System.Diagnostics;

namespace MDS.DatabaseBackupService.Services;

public sealed class BackupService
{
    private readonly BranchSettings _settings;
    private readonly BackupLogger _logger;

    public BackupService(BranchSettings settings, BackupLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<BackupResult> RunBackupAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_settings.LocalOutputDirectory);

        var safeBranchName = string.Concat(_settings.BranchName.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var fileName = $"{safeBranchName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
        var localBackupPath = Path.Combine(_settings.LocalOutputDirectory, fileName);

        _logger.Info($"Starting mysqldump for '{_settings.DatabaseName}' from '{_settings.Host}:{_settings.Port}'.");
        await CreateDumpAsync(localBackupPath, cancellationToken);
        _logger.Info($"Local dump created at '{localBackupPath}'.");

        try
        {
            Directory.CreateDirectory(_settings.GoogleDriveSyncDirectory);
            var driveBackupPath = Path.Combine(_settings.GoogleDriveSyncDirectory, fileName);
            File.Copy(localBackupPath, driveBackupPath, overwrite: true);
            _logger.Info($"Backup copied to Google Drive sync folder '{driveBackupPath}'.");

            CleanupOldLocalBackups();
            return new BackupResult(localBackupPath, driveBackupPath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Dump created locally but copy to Google Drive failed: {ex.Message}");
            throw new InvalidOperationException(
                $"Dump created at '{localBackupPath}', but Google Drive copy failed. {ex.Message}", ex);
        }
    }

    private async Task CreateDumpAsync(string outputPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.MysqldumpPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--single-transaction");
        startInfo.ArgumentList.Add("--routines");
        startInfo.ArgumentList.Add("--triggers");
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(_settings.Host);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(_settings.Port.ToString());
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add(_settings.Username);
        startInfo.ArgumentList.Add(_settings.DatabaseName);
        startInfo.Environment["MYSQL_PWD"] = _settings.Password;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start mysqldump.exe.");
        }

        await using var fileStream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true);

        var stdoutCopyTask = process.StandardOutput.BaseStream.CopyToAsync(fileStream, cancellationToken);
        var stderrReadTask = process.StandardError.ReadToEndAsync();

        await stdoutCopyTask;
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrReadTask;

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.Info(stderr.Trim());
        }

        if (process.ExitCode != 0)
        {
            TryDeleteFile(outputPath);
            throw new InvalidOperationException($"mysqldump exited with code {process.ExitCode}. {stderr}".Trim());
        }
    }

    private void CleanupOldLocalBackups()
    {
        var cutoff = DateTime.Now.AddDays(-_settings.RetentionDays);
        foreach (var file in Directory.EnumerateFiles(_settings.LocalOutputDirectory, "*.sql", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                info.Delete();
                _logger.Info($"Deleted old backup '{info.FullName}'.");
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

public sealed record BackupResult(string LocalBackupPath, string GoogleDriveBackupPath);
