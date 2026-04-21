using System.Collections.Concurrent;

namespace LogAnalyzerApp.Services;

public class UploadFileService
{
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    public string Register(string tempPath)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = tempPath;
        return token;
    }

    public string? GetAndRemove(string token)
    {
        _tokens.TryRemove(token, out var path);
        return path;
    }
}
