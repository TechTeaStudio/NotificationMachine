using System.Globalization;

namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>Mirrors the file name Serilog's RollingInterval.Day roller produces for a given day.</summary>
internal static class LogFileNaming
{
    internal static string BuildFileName(string logFileName, DateOnly date)
    {
        var stamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var extension = Path.GetExtension(logFileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(logFileName);
        return nameWithoutExtension + stamp + extension;
    }
}
