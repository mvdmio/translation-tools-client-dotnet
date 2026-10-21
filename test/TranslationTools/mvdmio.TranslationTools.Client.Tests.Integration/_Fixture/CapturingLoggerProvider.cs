using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace mvdmio.TranslationTools.Client.Tests.Integration._Fixture;

/// <summary>
/// Records every log line written through the host's logging, so a test can assert on what an
/// operator would see.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
   public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

   public ILogger CreateLogger(string categoryName)
   {
      return new CapturingLogger(categoryName, Entries);
   }

   public void Dispose()
   {
   }

   private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
   {
      public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

      public bool IsEnabled(LogLevel logLevel) => true;

      public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
      {
         entries.Enqueue(new CapturedLogEntry(category, logLevel, formatter(state, exception), exception));
      }
   }
}

internal sealed record CapturedLogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
