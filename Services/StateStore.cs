using System.Text.Json;

namespace MDS.DatabaseBackupService.Services;

public sealed class StateStore
{
    private readonly string _stateFilePath;

    public StateStore(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public AppState Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new AppState();
        }

        var json = File.ReadAllText(_stateFilePath);
        return JsonSerializer.Deserialize<AppState>(json, JsonOptions()) ?? new AppState();
    }

    public void Save(AppState state)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(state, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
}
