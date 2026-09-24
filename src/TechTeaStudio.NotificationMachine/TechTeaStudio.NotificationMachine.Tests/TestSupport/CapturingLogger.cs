using Microsoft.Extensions.Logging;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();
    private readonly object _gate = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (_gate)
        {
            _entries.Add((logLevel, message));
        }
    }

    public bool Any(LogLevel level, Func<string, bool> predicate)
    {
        lock (_gate)
        {
            return _entries.Any(e => e.Level == level && predicate(e.Message));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
