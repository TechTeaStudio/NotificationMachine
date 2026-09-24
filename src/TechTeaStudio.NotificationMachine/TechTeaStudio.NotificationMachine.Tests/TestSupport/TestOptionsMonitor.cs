using Microsoft.Extensions.Options;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
