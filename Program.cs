using MDS.DatabaseBackupService;
using MDS.DatabaseBackupService.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MDS-DatabaseBackup";
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
