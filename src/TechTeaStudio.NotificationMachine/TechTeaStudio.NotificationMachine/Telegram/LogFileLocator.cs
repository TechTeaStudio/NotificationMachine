namespace TechTeaStudio.NotificationMachine.Telegram;

internal static class LogFileLocator
{
    /// <summary>configured, if non-empty; otherwise cwd/logs, /logs, baseDir/logs, baseDir, evaluated at call time.</summary>
    internal static IReadOnlyList<string> ResolveDirectories(IList<string> configured)
    {
        if (configured.Count > 0)
            return configured.ToArray();

        return new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "logs"),
            "/logs",
            Path.Combine(AppContext.BaseDirectory, "logs"),
            AppContext.BaseDirectory
        };
    }

    /// <summary>The first directory (in order) containing fileName, or null if none do.</summary>
    internal static string? Find(IEnumerable<string> directories, string fileName)
    {
        foreach (var directory in directories)
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
