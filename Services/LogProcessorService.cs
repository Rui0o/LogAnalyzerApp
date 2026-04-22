using LogAnalyzerApp.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace LogAnalyzerApp.Services;

public enum ContextDirection { Before, After, Both }

public record LogAnalysisResult(
    List<string> Tags,
    List<string> Levels,
    Dictionary<string, int> LevelCounts,
    DeviceInfo DeviceInfo,
    List<string> Sections);

public class LogProcessorService
{
    // Device info extraction patterns
    private static readonly Regex RxBuildFingerprint = new(@"^Build fingerprint: '(.+)'$", RegexOptions.Compiled);
    private static readonly Regex RxDumpstateTime   = new(@"^== dumpstate: (.+)$", RegexOptions.Compiled);
    private static readonly Regex RxRoDebugLevel    = new(@"^\[ro\.debug_level\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxNetwork         = new(@"^Network: (.+)$", RegexOptions.Compiled);
    private static readonly Regex RxDebugLevel      = new(@"^androidboot\.debug_level = ""(0x[0-9a-fA-F]+)""", RegexOptions.Compiled);
    private static readonly Regex RxEmModel         = new(@"^androidboot\.em\.model = ""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex RxMemTotal        = new(@"^MemTotal:\s+(\d+) kB$", RegexOptions.Compiled);
    private static readonly Regex RxMemFree         = new(@"^MemFree:\s+(\d+) kB$", RegexOptions.Compiled);
    private static readonly Regex RxMemAvail        = new(@"^MemAvailable:\s+(\d+) kB$", RegexOptions.Compiled);
    private static readonly Regex RxDisplayInit     = new(@"\binit=(\d+x\d+ \d+dpi)\b", RegexOptions.Compiled);
    private static readonly Regex RxModemBoard      = new(@"^\[ril\.modem\.board[^\]]*\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxOsVersion       = new(@"^\[ro\.build\.version\.release\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxSdkVersion      = new(@"^\[ro\.build\.version\.sdk\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxHardware        = new(@"^\[ro\.hardware\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxDramInfo        = new(@"^\[ro\.boot\.dram_info\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxProductModel    = new(@"^\[ro\.product\.model\]: \[([^\]]+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxBattLevel       = new(@"^\s+level: (\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxBattStatus      = new(@"^\s+status: (\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxBattHealth      = new(@"^\s+health: (\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxBattVoltage     = new(@"^\s+voltage: (\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxBattTemp        = new(@"^\s+temperature: (\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxBattTech        = new(@"^\s+technology: (.+)$", RegexOptions.Compiled);

    public async Task<LogAnalysisResult> ExtractUniqueTagsAndLevelsAsync(Stream fileStream)
    {
        var tags = new HashSet<string>();
        var levels = new HashSet<string>();
        var levelCounts = new Dictionary<string, int>();
        var device = new DeviceInfo();
        var sections = new List<string>();
        bool inBatterySection = false;
        bool batteryDone = false;

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

            var sectionName = TryGetNewSection(line);
            if (sectionName != null) sections.Add(sectionName);

            CollectDeviceInfo(line, device, ref inBatterySection, ref batteryDone);
        }

        return new LogAnalysisResult(
            tags.OrderBy(t => t).ToList(),
            levels.OrderBy(l => l).ToList(),
            levelCounts,
            device,
            sections);
    }

    private static string? TryGetNewSection(string line)
    {
        if (!line.StartsWith("DUMP OF SERVICE ")) return null;
        var rest = line["DUMP OF SERVICE ".Length..];
        if (rest.StartsWith("CRITICAL ")) rest = rest["CRITICAL ".Length..];
        var name = rest.TrimEnd(':').Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static bool IsSectionEndMarker(string line) =>
        line.StartsWith("------") && line.Contains("was the duration of");

    private void CollectDeviceInfo(string line, DeviceInfo device, ref bool inBatterySection, ref bool batteryDone)
    {
        if (line.StartsWith("DUMP OF SERVICE "))
        {
            inBatterySection = !batteryDone && line == "DUMP OF SERVICE battery:";
            return;
        }

        Match m;

        if (device.DumpstateTime == null && (m = RxDumpstateTime.Match(line)).Success)
            { device.DumpstateTime = m.Groups[1].Value.Trim(); return; }

        if (inBatterySection && !batteryDone)
        {
            if (device.BatteryLevel == null && (m = RxBattLevel.Match(line)).Success)
                device.BatteryLevel = int.Parse(m.Groups[1].Value);
            if (device.BatteryStatus == null && (m = RxBattStatus.Match(line)).Success)
                device.BatteryStatus = MapBatteryStatus(int.Parse(m.Groups[1].Value));
            if (device.BatteryHealth == null && (m = RxBattHealth.Match(line)).Success)
                device.BatteryHealth = MapBatteryHealth(int.Parse(m.Groups[1].Value));
            if (device.BatteryVoltageMillivolts == null && (m = RxBattVoltage.Match(line)).Success)
                device.BatteryVoltageMillivolts = int.Parse(m.Groups[1].Value);
            if (device.BatteryTemperatureCelsius == null && (m = RxBattTemp.Match(line)).Success)
                device.BatteryTemperatureCelsius = int.Parse(m.Groups[1].Value) / 10.0;
            if (device.BatteryTechnology == null && (m = RxBattTech.Match(line)).Success)
                device.BatteryTechnology = m.Groups[1].Value.Trim();

            if (device.BatteryLevel != null && device.BatteryStatus != null &&
                device.BatteryHealth != null && device.BatteryVoltageMillivolts != null &&
                device.BatteryTemperatureCelsius != null && device.BatteryTechnology != null)
                batteryDone = true;
            return;
        }

        if (device.Model == null && (m = RxEmModel.Match(line)).Success)
            { device.Model = m.Groups[1].Value; return; }
        if (device.Binary == null && (m = RxBuildFingerprint.Match(line)).Success)
        {
            var fp = m.Groups[1].Value;
            var parts = fp.Split('/');
            device.Binary = parts.Length > 4 ? parts[4] : fp;
            return;
        }
        if (device.DebugLevel == null && (m = RxRoDebugLevel.Match(line)).Success)
            { device.DebugLevel = MapDebugLevel(m.Groups[1].Value); return; }
        if (device.Network == null && (m = RxNetwork.Match(line)).Success)
            { device.Network = m.Groups[1].Value.TrimEnd(',').Trim(); return; }
        if (device.MemTotalKb == null && (m = RxMemTotal.Match(line)).Success)
            { device.MemTotalKb = long.Parse(m.Groups[1].Value); return; }
        if (device.MemFreeKb == null && (m = RxMemFree.Match(line)).Success)
            { device.MemFreeKb = long.Parse(m.Groups[1].Value); return; }
        if (device.MemAvailableKb == null && (m = RxMemAvail.Match(line)).Success)
            { device.MemAvailableKb = long.Parse(m.Groups[1].Value); return; }
        if (device.Display == null && (m = RxDisplayInit.Match(line)).Success)
            { device.Display = m.Groups[1].Value; return; }
        if (device.Chipset == null && (m = RxModemBoard.Match(line)).Success)
            { device.Chipset = m.Groups[1].Value; return; }
        if (device.OsVersion == null && (m = RxOsVersion.Match(line)).Success)
            { device.OsVersion = m.Groups[1].Value; return; }
        if (device.SdkVersion == null && (m = RxSdkVersion.Match(line)).Success)
            { device.SdkVersion = m.Groups[1].Value; return; }
        if (device.Hardware == null && (m = RxHardware.Match(line)).Success)
            { device.Hardware = m.Groups[1].Value; return; }
        if (device.PhysicalRam == null && (m = RxDramInfo.Match(line)).Success)
            { device.PhysicalRam = ParseDramSize(m.Groups[1].Value); return; }
        if (device.Model == null && (m = RxProductModel.Match(line)).Success)
            { device.Model = m.Groups[1].Value; return; }
    }

    private static string MapDebugLevel(string hex) => hex.ToLowerInvariant() switch
    {
        "0x4f4c" => "LOW",
        "0x494d" => "MID",
        "0x4856" => "HIGH",
        _ => hex
    };

    private static string MapBatteryStatus(int s) => s switch
    {
        2 => "Charging",
        3 => "Discharging",
        4 => "Not charging",
        5 => "Full",
        _ => "Unknown"
    };

    private static string MapBatteryHealth(int h) => h switch
    {
        2 => "Good",
        3 => "Overheat",
        4 => "Dead",
        5 => "Over voltage",
        6 => "Unspecified failure",
        7 => "Cold",
        _ => "Unknown"
    };

    private static string ParseDramSize(string info)
    {
        // format: "01,08,00,8G" — last segment is size
        var last = info.LastIndexOf(',');
        return last >= 0 ? info[(last + 1)..].Trim() : info;
    }

    public async Task GenerateFilteredLogAsync(
        Stream inputFileStream,
        Stream outputFileStream,
        List<string> selectedTags,
        List<string> selectedLevels,
        int contextLines,
        ContextDirection direction = ContextDirection.Before,
        bool addSeparator = false,
        string? keywordFilter = null,
        List<string>? selectedSections = null)
    {
        using var reader = new StreamReader(inputFileStream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(outputFileStream, Encoding.UTF8,
            bufferSize: 4096, leaveOpen: true);

        var beforeBuffer = new Queue<string>(contextLines > 0 ? contextLines : 1);
        int afterRemaining = 0;
        bool needsSeparator = false;
        bool everWroteContent = false;
        string? currentSection = null;
        var keywords = string.IsNullOrWhiteSpace(keywordFilter)
            ? Array.Empty<string>()
            : keywordFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Where(k => !string.IsNullOrWhiteSpace(k))
                           .ToArray();
        bool hasSections = selectedSections is { Count: > 0 };

        bool wasInSelectedSection = false;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            // Track current section
            if (IsSectionEndMarker(line))
                currentSection = null;
            else
            {
                var sn = TryGetNewSection(line);
                if (sn != null)
                {
                    if (hasSections) { beforeBuffer.Clear(); afterRemaining = 0; }
                    currentSection = sn;
                }
            }

            bool isInSelectedSection = hasSections && currentSection != null && selectedSections!.Contains(currentSection);

            // Section lines: write directly, skip context logic
            if (isInSelectedSection)
            {
                if (addSeparator && everWroteContent && needsSeparator)
                {
                    await writer.WriteLineAsync("────────────────────────────────────────");
                    needsSeparator = false;
                }
                await writer.WriteLineAsync(line);
                everWroteContent = true;
                wasInSelectedSection = true;
                continue;
            }

            if (wasInSelectedSection && everWroteContent) needsSeparator = true;
            wasInSelectedSection = false;

            var entry = LogEntry.Parse(line);
            bool isMatch = (selectedTags.Count > 0 && selectedTags.Contains(entry.Tag))
                        || (selectedLevels.Count > 0 && selectedLevels.Contains(entry.Level))
                        || (keywords.Length > 0 && keywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase)));

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
