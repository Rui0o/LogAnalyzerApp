using System.Collections.Concurrent;

namespace LogAnalyzerApp.Services;

/// <summary>
/// Manages single-use download tokens that map to temporary output files.
/// Registered as a singleton so tokens survive across Blazor circuits.
/// </summary>
public class DownloadFileService
{
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    public string RegisterFile(string filePath)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = filePath;
        return token;
    }

    /// <summary>Returns the file path and removes the token (single-use).</summary>
    public string? GetAndRemove(string token)
    {
        _tokens.TryRemove(token, out var path);
        return path;
    }
}
