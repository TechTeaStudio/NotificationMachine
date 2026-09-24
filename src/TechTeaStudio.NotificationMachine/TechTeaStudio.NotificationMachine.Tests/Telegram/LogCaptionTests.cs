using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class LogCaptionTests
{
    [Fact]
    public void Build_TemplateWithBothPlaceholders_ReplacesServiceAndDate()
    {
        var result = LogCaption.Build("{Service} daily log for {Date}", "MyService", new DateOnly(2026, 9, 23));

        Assert.Equal("MyService daily log for 2026-09-23", result);
    }

    [Fact]
    public void Build_TemplateWithoutPlaceholders_ReturnsTemplateUnchanged()
    {
        var result = LogCaption.Build("Static caption text", "MyService", new DateOnly(2026, 9, 23));

        Assert.Equal("Static caption text", result);
    }

    [Fact]
    public void ResolveServiceName_NullServiceNameAndNullEntryAssemblyName_ReturnsApp()
    {
        var result = LogCaption.ResolveServiceName(null, null);

        Assert.Equal("app", result);
    }

    [Fact]
    public void ResolveServiceName_BlankServiceName_FallsBackToEntryAssemblyName()
    {
        var result = LogCaption.ResolveServiceName("   ", "EntryAssemblyName");

        Assert.Equal("EntryAssemblyName", result);
    }

    [Fact]
    public void ResolveServiceName_ExplicitServiceName_Wins()
    {
        var result = LogCaption.ResolveServiceName("MyService", "EntryAssemblyName");

        Assert.Equal("MyService", result);
    }

    [Fact]
    public void Build_LongerThanTelegramLimit_TruncatedTo1024Characters()
    {
        var template = new string('x', 2000);

        var result = LogCaption.Build(template, "MyService", new DateOnly(2026, 9, 23));

        Assert.Equal(1024, result.Length);
        Assert.Equal(new string('x', 1024), result);
    }
}
