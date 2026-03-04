namespace LogAnalyzerApp.Models;

public class LogEntry
{
    public string RawLine { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    private static readonly Dictionary<string, string> LevelMapping = new()
    {
        { " V ", "Verbose" },
        { " D ", "Debug" },
        { " I ", "Information" },
        { " W ", "Warning" },
        { " E ", "Error" },
        { " F ", "Fatal" },
        { " FATAL ", "Fatal" }
    };

    public static LogEntry Parse(string line)
    {
        var entry = new LogEntry { RawLine = line };
        if (string.IsNullOrWhiteSpace(line)) return entry;

        int levelIndex = -1;
        string foundKey = "";

        foreach (var key in LevelMapping.Keys)
        {
            levelIndex = line.IndexOf(key);
            if (levelIndex != -1)
            {
                foundKey = key;
                break;
            }
        }

        if (levelIndex != -1)
        {
            entry.Level = LevelMapping[foundKey];
            entry.Timestamp = line.Substring(0, Math.Min(levelIndex, 19)).Trim();

            int tagStart = levelIndex + foundKey.Length;
            int colonIndex = line.IndexOf(':', tagStart);

            if (colonIndex != -1)
            {
                entry.Tag = line.Substring(tagStart, colonIndex - tagStart).Trim();
                entry.Message = line.Substring(colonIndex + 1).Trim();
            }
        }

        return entry;
    }
}
