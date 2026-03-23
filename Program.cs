using MDS.DatabaseBackupService;
using MDS.DatabaseBackupService.Services;

if (args.Contains("--run-now", StringComparer.OrdinalIgnoreCase))
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    var settings = BranchSettings.LoadOrCreate(configPath);
    var logger = new BackupLogger(settings);
    var svc = new BackupService(settings, logger);

    Console.WriteLine("Processing...");
    try
    {
        var result = await svc.RunBackupAsync(CancellationToken.None);
        Console.WriteLine($"Done: {result.LocalBackupPath}");
        Console.WriteLine($"Synced: {result.GoogleDriveBackupPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed: {ex.Message}");
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
    var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    return BranchSettings.LoadOrCreate(configPath);
});

builder.Services.AddSingleton<BackupLogger>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupWorker>();

var host = builder.Build();
host.Run();
