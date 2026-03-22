namespace LogAnalyzerApp.Services;

public class AppStateService
{
    public string FileName { get; private set; } = "";
    public int TagCount { get; private set; }
    public int LevelCount { get; private set; }

    public event Action? OnChange;

    public void SetFileInfo(string fileName, int tagCount, int levelCount)
    {
        FileName = fileName;
        TagCount = tagCount;
        LevelCount = levelCount;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        FileName = "";
        TagCount = 0;
        LevelCount = 0;
        OnChange?.Invoke();
    }
}
