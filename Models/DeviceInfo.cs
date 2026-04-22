namespace LogAnalyzerApp.Models;

public class DeviceInfo
{
    // Device
    public string? Model { get; set; }
    public string? Binary { get; set; }
    public string? DebugLevel { get; set; }
    public string? Chipset { get; set; }
    public string? Hardware { get; set; }
    public string? Display { get; set; }
    public string? Network { get; set; }
    public string? OsVersion { get; set; }
    public string? SdkVersion { get; set; }

    // Memory
    public string? PhysicalRam { get; set; }
    public long? MemTotalKb { get; set; }
    public long? MemFreeKb { get; set; }
    public long? MemAvailableKb { get; set; }

    // Dumpstate metadata
    public string? DumpstateTime { get; set; }

    // Battery
    public int? BatteryLevel { get; set; }
    public string? BatteryStatus { get; set; }
    public string? BatteryHealth { get; set; }
    public int? BatteryVoltageMillivolts { get; set; }
    public double? BatteryTemperatureCelsius { get; set; }
    public string? BatteryTechnology { get; set; }

    public bool HasData => Model != null || OsVersion != null || MemTotalKb != null || BatteryLevel != null;

    public static string FormatKb(long kb)
    {
        if (kb >= 1024L * 1024) return $"{kb / 1024 / 1024} GB";
        if (kb >= 1024) return $"{kb / 1024} MB";
        return $"{kb} kB";
    }
}
