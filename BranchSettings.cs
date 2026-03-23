using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDS.DatabaseBackupService;

public sealed class BranchSettings
{
    public string BranchName { get; set; } = "branch1";

    public string DatabaseName { get; set; } = "your_database_name";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 3306;

    public string Username { get; set; } = "root";

    public string Password { get; set; } = "CHANGE_ME";

    public string MysqldumpPath { get; set; } = string.Empty;

    public string RootFolder { get; set; } = string.Empty;

    public string LocalOutputDirectory { get; set; } = "Cache";

    public string GoogleDriveSyncDirectory { get; set; } = string.Empty;

    public string LogFilePath { get; set; } = Path.Combine("Logs", "update.log");

    public int RetentionDays { get; set; } = 14;

    public string ScheduleTime { get; set; } = "10:00 PM";

    public int ScheduleIntervalDays { get; set; } = 2;

    [JsonIgnore]
    public string StateFilePath => Path.Combine(RootFolder, "State", "state.json");

    public static BranchSettings LoadOrCreate(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            var template = CreateTemplate();
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, Serialize(template));
            throw new InvalidOperationException(
                $"A new config file was created at '{fullPath}'. Update config.json with your details, then restart the service.");
        }

        var json = File.ReadAllText(fullPath);
        var settings = JsonSerializer.Deserialize<BranchSettings>(json, JsonOptions())
            ?? throw new InvalidOperationException("config.json is empty or invalid.");

        settings.Normalize(fullPath);
        settings.Validate();
        return settings;
    }

    public TimeOnly GetScheduleTime()
    {
        if (TimeOnly.TryParse(ScheduleTime, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var time))
        {
            return time;
        }

        if (TimeOnly.TryParse(ScheduleTime, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out time))
        {
            return time;
        }

        throw new InvalidOperationException("ScheduleTime must be a valid time like '10:00 PM' or '22:00'.");
    }

    private void Normalize(string configPath)
    {
        RootFolder = ResolveRootFolder(configPath, RootFolder);
        LocalOutputDirectory = ResolvePath(LocalOutputDirectory, Path.Combine(RootFolder, "Cache"), RootFolder);
        LogFilePath = ResolvePath(LogFilePath, Path.Combine(RootFolder, "Logs", "update.log"), RootFolder);
        MysqldumpPath = ResolveOptionalPath(MysqldumpPath, RootFolder);
        GoogleDriveSyncDirectory = ResolveOptionalPath(GoogleDriveSyncDirectory, RootFolder);

        if (Port <= 0) Port = 3306;
        if (RetentionDays <= 0) RetentionDays = 14;
        if (ScheduleIntervalDays <= 0) ScheduleIntervalDays = 2;
        if (string.IsNullOrWhiteSpace(ScheduleTime)) ScheduleTime = "10:00 PM";
    }

    private void Validate()
    {
        var missing = new List<string>();
        AddIfMissing(BranchName, nameof(BranchName), missing);
        AddIfMissing(DatabaseName, nameof(DatabaseName), missing);
        AddIfMissing(Host, nameof(Host), missing);
        AddIfMissing(Username, nameof(Username), missing);
        AddIfMissing(MysqldumpPath, nameof(MysqldumpPath), missing);
        AddIfMissing(LocalOutputDirectory, nameof(LocalOutputDirectory), missing);
        AddIfMissing(GoogleDriveSyncDirectory, nameof(GoogleDriveSyncDirectory), missing);
        AddIfMissing(LogFilePath, nameof(LogFilePath), missing);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"config.json is missing: {string.Join(", ", missing)}");
        }

        if (!File.Exists(MysqldumpPath))
        {
            throw new InvalidOperationException($"Required tool was not found at '{MysqldumpPath}'.");
        }

        _ = GetScheduleTime();
    }

    private static BranchSettings CreateTemplate() =>
        new()
        {
            BranchName = "branch1",
            DatabaseName = "mdsbillingdbv5",
            Host = "localhost",
            Port = 3306,
            Username = "root",
            Password = "CHANGE_ME",
            MysqldumpPath = DetectMySqlDumpPath(),
            RootFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WinUpdateHelper"),
            LocalOutputDirectory = "Cache",
            GoogleDriveSyncDirectory = "",
            LogFilePath = Path.Combine("Logs", "update.log"),
            RetentionDays = 14,
            ScheduleTime = "10:00 PM",
            ScheduleIntervalDays = 2
        };

    private static string DetectMySqlDumpPath()
    {
        var candidates = new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Workbench 8.0 CE\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 5.7\bin\mysqldump.exe"
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static string ResolveRootFolder(string configPath, string configuredRoot)
    {
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? AppContext.BaseDirectory;
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinUpdateHelper")
            : Environment.ExpandEnvironmentVariables(configuredRoot);

        return Path.IsPathRooted(root)
            ? Path.GetFullPath(root)
            : Path.GetFullPath(Path.Combine(baseDirectory, root));
    }

    private static string ResolvePath(string configuredPath, string defaultPath, string rootFolder)
    {
        var effectivePath = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : Environment.ExpandEnvironmentVariables(configuredPath);

        return Path.IsPathRooted(effectivePath)
            ? Path.GetFullPath(effectivePath)
            : Path.GetFullPath(Path.Combine(rootFolder, effectivePath));
    }

    private static string ResolveOptionalPath(string configuredPath, string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return string.Empty;

        var effectivePath = Environment.ExpandEnvironmentVariables(configuredPath);
        return Path.IsPathRooted(effectivePath)
            ? Path.GetFullPath(effectivePath)
            : Path.GetFullPath(Path.Combine(rootFolder, effectivePath));
    }

    private static void AddIfMissing(string value, string name, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value)) missing.Add(name);
    }

    private static JsonSerializerOptions JsonOptions() =>
        new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };

    private static string Serialize(BranchSettings settings) =>
        JsonSerializer.Serialize(settings, JsonOptions());
}
