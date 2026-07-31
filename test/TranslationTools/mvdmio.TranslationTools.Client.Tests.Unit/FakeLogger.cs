using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// Records every log line written through it, so a test can assert on what an operator
/// would see without reaching into the client's internals.
/// </summary>
internal sealed class FakeLogger : ILogger
{
   public ConcurrentBag<FakeLogEntry> Entries { get; } = new();

   public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

   public bool IsEnabled(LogLevel logLevel) => true;

   public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
   {
      Entries.Add(new FakeLogEntry(logLevel, formatter(state, exception), exception));
   }
}

internal sealed record FakeLogEntry(LogLevel Level, string Message, Exception? Exception);
