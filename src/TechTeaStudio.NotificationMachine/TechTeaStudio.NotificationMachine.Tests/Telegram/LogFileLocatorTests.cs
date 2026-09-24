using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class LogFileLocatorTests
{
    [Fact]
    public void ResolveDirectories_ConfiguredNonEmpty_ReturnsConfiguredAsIs()
    {
        var configured = new List<string> { "/one", "/two" };

        var result = LogFileLocator.ResolveDirectories(configured);

        Assert.Equal(new[] { "/one", "/two" }, result);
    }

    [Fact]
    public void ResolveDirectories_Empty_ReturnsDefaultsInOrder()
    {
        var result = LogFileLocator.ResolveDirectories(new List<string>());

        Assert.Equal(
            new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "logs"),
                "/logs",
                Path.Combine(AppContext.BaseDirectory, "logs"),
                AppContext.BaseDirectory
            },
            result);
    }

    [Fact]
    public void Find_NoDirectoriesContainFile_ReturnsNull()
    {
        var directories = new[]
        {
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        };

        var result = LogFileLocator.Find(directories, "log20260923.txt");

        Assert.Null(result);
    }

    [Fact]
    public void Find_FileExistsInOneDirectory_ReturnsItsPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var missingDir = Path.Combine(root, "missing");
        var presentDir = Path.Combine(root, "present");
        Directory.CreateDirectory(presentDir);
        const string fileName = "log20260923.txt";
        var expectedPath = Path.Combine(presentDir, fileName);
        File.WriteAllText(expectedPath, "log");

        try
        {
            var result = LogFileLocator.Find(new[] { missingDir, presentDir }, fileName);

            Assert.Equal(expectedPath, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_FileExistsInMultipleDirectories_ReturnsFirstMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var firstDir = Path.Combine(root, "first");
        var secondDir = Path.Combine(root, "second");
        Directory.CreateDirectory(firstDir);
        Directory.CreateDirectory(secondDir);
        const string fileName = "log20260923.txt";
        var firstPath = Path.Combine(firstDir, fileName);
        var secondPath = Path.Combine(secondDir, fileName);
        File.WriteAllText(firstPath, "log1");
        File.WriteAllText(secondPath, "log2");

        try
        {
            var result = LogFileLocator.Find(new[] { firstDir, secondDir }, fileName);

            Assert.Equal(firstPath, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
