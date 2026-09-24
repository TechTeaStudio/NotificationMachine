using System.Globalization;
using System.Reflection;

namespace TechTeaStudio.NotificationMachine.Telegram;

internal static class LogCaption
{
    internal const int TelegramCaptionLimit = 1024;

    internal static string Build(string template, string? serviceName, DateOnly date)
    {
        var caption = template
            .Replace("{Service}", ResolveServiceName(serviceName))
            .Replace("{Date}", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return caption.Length > TelegramCaptionLimit ? caption[..TelegramCaptionLimit] : caption;
    }

    internal static string ResolveServiceName(string? serviceName) =>
        ResolveServiceName(serviceName, Assembly.GetEntryAssembly()?.GetName().Name);

    internal static string ResolveServiceName(string? serviceName, string? entryAssemblyName) =>
        !string.IsNullOrWhiteSpace(serviceName) ? serviceName : entryAssemblyName ?? "app";
}
