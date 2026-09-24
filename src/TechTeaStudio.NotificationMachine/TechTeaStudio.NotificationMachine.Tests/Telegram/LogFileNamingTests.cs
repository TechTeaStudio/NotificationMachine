using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class LogFileNamingTests
{
    [Fact]
    public void BuildFileName_SimpleFileName_InsertsDateBeforeExtension()
    {
        var result = LogFileNaming.BuildFileName("log.txt", new DateOnly(2026, 9, 23));

        Assert.Equal("log20260923.txt", result);
    }

    [Fact]
    public void BuildFileName_PrefixEndingWithDash_InsertsDateBeforeExtension()
    {
        var result = LogFileNaming.BuildFileName("authservice-.txt", new DateOnly(2026, 9, 23));

        Assert.Equal("authservice-20260923.txt", result);
    }

    [Fact]
    public void BuildFileName_NoExtension_AppendsDate()
    {
        var result = LogFileNaming.BuildFileName("app", new DateOnly(2026, 9, 23));

        Assert.Equal("app20260923", result);
    }
}
