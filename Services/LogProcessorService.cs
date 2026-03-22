using LogAnalyzerApp.Models;
using System.Text;

namespace LogAnalyzerApp.Services;

public enum ContextDirection { Before, After, Both }

public class LogProcessorService
{
    public async Task<(List<string> Tags, List<string> Levels, Dictionary<string, int> LevelCounts)> ExtractUniqueTagsAndLevelsAsync(Stream fileStream)
    {
        var tags = new HashSet<string>();
        var levels = new HashSet<string>();
        var levelCounts = new Dictionary<string, int>();

        using var reader = new StreamReader(fileStream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);
            if (!string.IsNullOrEmpty(entry.Tag)) tags.Add(entry.Tag);
            if (!string.IsNullOrEmpty(entry.Level))
            {
                levels.Add(entry.Level);
                levelCounts[entry.Level] = levelCounts.GetValueOrDefault(entry.Level) + 1;
            }
        }

        return (tags.OrderBy(t => t).ToList(), levels.OrderBy(l => l).ToList(), levelCounts);
    }

    public async Task GenerateFilteredLogAsync(
        Stream inputFileStream,
        Stream outputFileStream,
        List<string> selectedTags,
        List<string> selectedLevels,
        int contextLines,
        ContextDirection direction = ContextDirection.Before,
        bool addSeparator = false)
    {
        using var reader = new StreamReader(inputFileStream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(outputFileStream, Encoding.UTF8,
            bufferSize: 4096, leaveOpen: true);

        var beforeBuffer = new Queue<string>(contextLines > 0 ? contextLines : 1);
        int afterRemaining = 0;
        bool needsSeparator = false;
        bool everWroteContent = false;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);
            bool isMatch = (selectedTags.Count > 0 && selectedTags.Contains(entry.Tag))
                        || (selectedLevels.Count > 0 && selectedLevels.Contains(entry.Level));

            if (isMatch)
            {
                if (addSeparator && everWroteContent && needsSeparator)
                {
                    await writer.WriteLineAsync("────────────────────────────────────────");
                    needsSeparator = false;
                }

                if (direction == ContextDirection.Before || direction == ContextDirection.Both)
                {
                    while (beforeBuffer.Count > 0)
                        await writer.WriteLineAsync(beforeBuffer.Dequeue());
                }
                else
                {
                    beforeBuffer.Clear();
                }

                await writer.WriteLineAsync(line);
                everWroteContent = true;

                if (direction == ContextDirection.After || direction == ContextDirection.Both)
                    afterRemaining = contextLines;
            }
            else if (afterRemaining > 0)
            {
                await writer.WriteLineAsync(line);
                afterRemaining--;

                if (afterRemaining == 0)
                    needsSeparator = true;

                if (direction == ContextDirection.Both && contextLines > 0)
                {
                    if (beforeBuffer.Count >= contextLines) beforeBuffer.Dequeue();
                    beforeBuffer.Enqueue(line);
                }
            }
            else
            {
                if (direction == ContextDirection.Before || direction == ContextDirection.Both)
                {
                    if (contextLines > 0)
                    {
                        if (beforeBuffer.Count >= contextLines)
                        {
                            beforeBuffer.Dequeue();
                            if (everWroteContent) needsSeparator = true;
                        }
                        beforeBuffer.Enqueue(line);
                    }
                    else if (everWroteContent)
                    {
                        needsSeparator = true;
                    }
                }
                else if (everWroteContent)
                {
                    needsSeparator = true;
                }
            }
        }

        await writer.FlushAsync();
    }
}
