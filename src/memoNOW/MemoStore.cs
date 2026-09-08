using System.IO;
using System.Text.Json;

namespace memoNOW;

public sealed class MemoStore
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public MemoStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDirectory = Path.Combine(appData, "memoNOW");
        _dataPath = Path.Combine(dataDirectory, "memos.json");
    }

    public IReadOnlyList<MemoItem> Load()
    {
        if (!File.Exists(_dataPath))
        {
            return Array.Empty<MemoItem>();
        }

        try
        {
            var json = File.ReadAllText(_dataPath);
            var items = JsonSerializer.Deserialize<List<MemoItem>>(json, _jsonOptions) ?? new List<MemoItem>();

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .OrderByDescending(item => item.CreatedAt)
                .Take(10)
                .ToList();
        }
        catch (JsonException)
        {
            PreserveBrokenFile();
            return Array.Empty<MemoItem>();
        }
        catch (IOException)
        {
            return Array.Empty<MemoItem>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<MemoItem>();
        }
    }

    public void Save(IEnumerable<MemoItem> items)
    {
        var directory = Path.GetDirectoryName(_dataPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(items, _jsonOptions);
        var tempPath = _dataPath + ".tmp";

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _dataPath, overwrite: true);
    }

    private void PreserveBrokenFile()
    {
        try
        {
            var brokenPath = _dataPath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(_dataPath, brokenPath, overwrite: true);
        }
        catch
        {
            // A corrupt scratchpad should never prevent memoNOW from starting.
        }
    }
}
