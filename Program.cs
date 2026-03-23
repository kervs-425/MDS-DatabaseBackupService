using MDS.DatabaseBackupService;
using MDS.DatabaseBackupService.Services;

// --backup-now: run a single backup and exit (no service)
if (args.Contains("--backup-now", StringComparer.OrdinalIgnoreCase))
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "branchsettings.json");
    var settings = BranchSettings.LoadOrCreate(configPath);
    var logger = new BackupLogger(settings);
    var backupService = new BackupService(settings, logger);

    Console.WriteLine("Starting backup...");
    try
    {
        var result = await backupService.RunBackupAsync(CancellationToken.None);
        Console.WriteLine($"Backup completed: {result.LocalBackupPath}");
        Console.WriteLine($"Copied to: {result.GoogleDriveBackupPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Backup failed: {ex.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WinUpdateHelper";
});

builder.Services.AddSingleton<BranchSettings>(_ =>
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "branchsettings.json");
    return BranchSettings.LoadOrCreate(configPath);
});

builder.Services.AddSingleton<BackupLogger>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupWorker>();

var host = builder.Build();
host.Run();
