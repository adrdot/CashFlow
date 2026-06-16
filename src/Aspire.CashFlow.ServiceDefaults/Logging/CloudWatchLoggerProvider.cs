using Microsoft.Extensions.Logging;

namespace Aspire.CashFlow.ServiceDefaults.Logging;

internal sealed class CloudWatchLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly CloudWatchLogWriter _writer;
    private IExternalScopeProvider? _scopeProvider;

    public CloudWatchLoggerProvider(CloudWatchLogWriter writer)
    {
        _writer = writer;
    }

    public ILogger CreateLogger(string categoryName) =>
        new CloudWatchLogger(categoryName, _writer, () => _scopeProvider);

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider;

    private sealed class CloudWatchLogger : ILogger
    {
        private readonly string _category;
        private readonly CloudWatchLogWriter _writer;
        private readonly Func<IExternalScopeProvider?> _scopeProvider;

        public CloudWatchLogger(
            string category,
            CloudWatchLogWriter writer,
            Func<IExternalScopeProvider?> scopeProvider
        )
        {
            _category = category;
            _writer = writer;
            _scopeProvider = scopeProvider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => _scopeProvider()?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _writer.Enqueue(logLevel, _category, eventId, formatter(state, exception), exception);
        }
    }
}
