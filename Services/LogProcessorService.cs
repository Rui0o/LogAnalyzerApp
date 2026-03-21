using LogAnalyzerApp.Models;
using System.Text;

namespace LogAnalyzerApp.Services;

public class LogProcessorService
{
    public async Task<(List<string> Tags, List<string> Levels)> ExtractUniqueTagsAndLevelsAsync(Stream fileStream)
    {
        var tags = new HashSet<string>();
        var levels = new HashSet<string>();

        // leaveOpen: true — the caller owns the stream lifetime; we must not close it
        using var reader = new StreamReader(fileStream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);
            if (!string.IsNullOrEmpty(entry.Tag)) tags.Add(entry.Tag);
            if (!string.IsNullOrEmpty(entry.Level)) levels.Add(entry.Level);
        }

        return (tags.OrderBy(t => t).ToList(), levels.OrderBy(l => l).ToList());
    }

    public async Task GenerateFilteredLogAsync(
        Stream inputFileStream,
        Stream outputFileStream,
        List<string> selectedTags,
        List<string> selectedLevels,
        int contextLines)
    {
        // leaveOpen: true — callers control the lifetime of both streams
        using var reader = new StreamReader(inputFileStream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(outputFileStream, Encoding.UTF8,
            bufferSize: 4096, leaveOpen: true);

        var buffer = new Queue<string>(contextLines > 0 ? contextLines : 1);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);

            bool tagMatch = selectedTags.Count > 0 && selectedTags.Contains(entry.Tag);
            bool levelMatch = selectedLevels.Count > 0 && selectedLevels.Contains(entry.Level);

            if (tagMatch || levelMatch)
            {
                while (buffer.Count > 0)
                {
                    await writer.WriteLineAsync(buffer.Dequeue());
                }
                await writer.WriteLineAsync(line);
            }
            else
            {
                if (contextLines > 0)
                {
                    if (buffer.Count >= contextLines)
                    {
                        buffer.Dequeue();
                    }
                    buffer.Enqueue(line);
                }
            }
        }

        await writer.FlushAsync();
    }
}
