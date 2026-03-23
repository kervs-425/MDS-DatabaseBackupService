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

        var safeName = string.Concat(_settings.BranchName.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
        var localPath = Path.Combine(_settings.LocalOutputDirectory, fileName);

        _logger.Info($"Task started for '{_settings.BranchName}'.");
        await RunProcessAsync(localPath, cancellationToken);
        _logger.Info($"Task completed. Output: '{localPath}'.");

        try
        {
            Directory.CreateDirectory(_settings.GoogleDriveSyncDirectory);
            var syncPath = Path.Combine(_settings.GoogleDriveSyncDirectory, fileName);
            File.Copy(localPath, syncPath, overwrite: true);
            _logger.Info($"Synced to '{syncPath}'.");

            CleanupOldFiles();
            return new BackupResult(localPath, syncPath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Sync failed: {ex.Message}");
            throw new InvalidOperationException(
                $"Output created at '{localPath}', but sync failed. {ex.Message}", ex);
        }
    }

    private async Task RunProcessAsync(string outputPath, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Failed to start required process.");
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
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}. {stderr}".Trim());
        }
    }

    private void CleanupOldFiles()
    {
        var cutoff = DateTime.Now.AddDays(-_settings.RetentionDays);
        foreach (var file in Directory.EnumerateFiles(_settings.LocalOutputDirectory, "*.dat", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                info.Delete();
                _logger.Info($"Cleaned up '{info.Name}'.");
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

public sealed record BackupResult(string LocalBackupPath, string GoogleDriveBackupPath);
