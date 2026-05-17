using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ReDotnet.Server.Cli;

/// Writes one JSON object per log entry, newline-delimited. Used for the
/// `--log-file` CLI flag. No console output — stdout/stderr are reserved
/// for the MCP transport.
public sealed class JsonLineFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _gate = new();

    public JsonLineFileLoggerProvider(string path) => _path = path;

    public ILogger CreateLogger(string categoryName) => new JsonLineLogger(this, categoryName);

    public void Dispose() { }

    internal void Write(string category, LogLevel level, EventId eventId, string message, Exception? ex)
    {
        var record = new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            level = level.ToString(),
            category,
            eventId = eventId.Id,
            message,
            exception = ex?.ToString(),
        };
        var line = JsonSerializer.Serialize(record);
        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    private sealed class JsonLineLogger : ILogger
    {
        private readonly JsonLineFileLoggerProvider _owner;
        private readonly string _category;

        public JsonLineLogger(JsonLineFileLoggerProvider owner, string category)
        {
            _owner = owner;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            _owner.Write(_category, level, eventId, formatter(state, exception), exception);
        }
    }
}
