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
    };

    // Logcat format: "MM-DD HH:MM:SS.mmm  PID  TID L Tag: Message"
    // The level character always appears in the header section (before the tag name),
    // never beyond ~50 characters. Limiting the search prevents false matches when
    // the message body happens to contain tokens like " E " or " W ".
    private const int MaxHeaderLength = 50;

    public static LogEntry Parse(string line)
    {
        var entry = new LogEntry { RawLine = line };
        if (string.IsNullOrWhiteSpace(line)) return entry;

        int levelIndex = -1;
        string foundKey = "";

        int searchLen = Math.Min(line.Length, MaxHeaderLength);

        foreach (var key in LevelMapping.Keys)
        {
            int idx = line.IndexOf(key, 0, searchLen);
            if (idx != -1)
            {
                levelIndex = idx;
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
