using LogAnalyzerApp.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LogAnalyzerApp.Services;

public class LogProcessorService
{
    public async Task<(List<string> Tags, List<string> Levels)> ExtractUniqueTagsAndLevelsAsync(Stream fileStream)
    {
        var tags = new HashSet<string>();
        var levels = new HashSet<string>();

        using var reader = new StreamReader(fileStream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);
            if (!string.IsNullOrEmpty(entry.Tag)) tags.Add(entry.Tag);
            if (!string.IsNullOrEmpty(entry.Level)) levels.Add(entry.Level);
        }

        return (tags.OrderBy(t => t).ToList(), levels.OrderBy(l => l).ToList());
    }

    public async Task GenerateFilteredLogAsync(Stream inputFileStream, Stream outputFileStream, List<string> selectedTags, List<string> selectedLevels, int contextLines)
    {
        using var reader = new StreamReader(inputFileStream);
        using var writer = new StreamWriter(outputFileStream);
        
        var buffer = new Queue<string>(contextLines > 0 ? contextLines : 1);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            var entry = LogEntry.Parse(line);
            
            bool tagMatch = selectedTags.Any() && selectedTags.Contains(entry.Tag);
            bool levelMatch = selectedLevels.Any() && selectedLevels.Contains(entry.Level);
            
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
