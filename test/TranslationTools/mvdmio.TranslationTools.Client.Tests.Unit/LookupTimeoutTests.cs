using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using mvdmio.TranslationTools.Client.Internal;
using System.Globalization;
using System.Net;
using Xunit;

namespace mvdmio.TranslationTools.Client.Tests.Unit;

/// <summary>
/// A single-key lookup is bounded by <see cref="TranslationToolsClientOptions.LookupTimeout"/>,
/// applied as a <see cref="TimeProvider"/>-derived cancellation token so a <see cref="FakeTimeProvider"/>
/// can drive it deterministically, rather than by setting <see cref="HttpClient.Timeout"/>. A whole-locale
/// lookup is not bounded by it.
/// </summary>
public class LookupTimeoutTests
{
   private const string ProjectOriginPrefix = "Fixture.App:";

   [Fact]
   public async Task Lookup_HandlerNeverCompletes_ShouldTimeoutAtDefaultFiveSeconds_AndReturnFallback()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new NeverCompletingHandler(), time);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      time.Advance(TimeSpan.FromSeconds(5));

      var response = await task;
      response.Value.Should().Be("Save");
   }

   [Fact]
   public async Task LookupTimeout_CustomValue_ShouldOverrideDefault()
   {
      var time = new FakeTimeProvider();
      var customTimeout = TimeSpan.FromMilliseconds(200);
      using var client = CreateClient(new NeverCompletingHandler(), time, lookupTimeout: customTimeout);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      time.Advance(customTimeout);

      var response = await task;
      response.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_ShouldNotModifyHttpClientTimeout()
   {
      using var httpClient = new HttpClient(new NeverCompletingHandler());
      var originalTimeout = httpClient.Timeout;

      using var client = new TranslationToolsClient(
         httpClient,
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            LookupTimeout = TimeSpan.FromMilliseconds(50)
         }),
         new LocalTranslationToolsClientCache(),
         new FakeTimeProvider()
      );

      httpClient.Timeout.Should().Be(originalTimeout);
   }

   [Fact]
   public async Task Lookup_HandlerSendsHeadersThenStallsTheBody_ShouldStillTimeout_AndReturnFallback()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new StallingBodyHandler(), time);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      time.Advance(TimeSpan.FromSeconds(5));

      var response = await task;
      response.Value.Should().Be("Save");
   }

   [Fact]
   public async Task Lookup_TimeoutWithThrowOnLookupError_ShouldThrowTranslationLookupException()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new NeverCompletingHandler(), time, throwOnLookupError: true);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      time.Advance(TimeSpan.FromSeconds(5));

      var act = async () => await task;
      await act.Should().ThrowAsync<TranslationLookupException>();
   }

   [Fact]
   public async Task Lookup_Timeout_ShouldLogLikeAnyOtherDegradedLookup()
   {
      var time = new FakeTimeProvider();
      var logger = new FakeLogger();
      using var client = CreateClient(new NeverCompletingHandler(), time, logger: logger);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, TestContext.Current.CancellationToken);

      time.Advance(TimeSpan.FromSeconds(5));
      await task;

      var entry = logger.Entries.Single();
      entry.Level.Should().Be(LogLevel.Warning);
      entry.Message.Should().Contain("Button.Save");
   }

   [Fact]
   public async Task Lookup_CallersCancellation_ShouldPropagate_RatherThanReportAsTimeout()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new NeverCompletingHandler(), time);
      var translation = new TranslationRef(ProjectOriginPrefix + "/Localizations.resx", "Button.Save");
      using var cts = new CancellationTokenSource();

      var task = client.GetAsync(translation, new CultureInfo("en"), defaultValue: "Save", localeValues: null, cts.Token);

      await cts.CancelAsync();

      var act = async () => await task;
      await act.Should().ThrowAsync<OperationCanceledException>();
   }

   [Fact]
   public async Task GetLocaleAsync_ShouldNotBeBoundedByLookupTimeout()
   {
      var time = new FakeTimeProvider();
      using var client = CreateClient(new NeverCompletingHandler(), time, lookupTimeout: TimeSpan.FromMilliseconds(1));
      using var cts = new CancellationTokenSource();

      var task = client.GetLocaleAsync(new CultureInfo("en"), cts.Token);

      // Advance the clock well past LookupTimeout; a whole-locale lookup must not observe it.
      time.Advance(TimeSpan.FromSeconds(10));
      await Task.Delay(50, TestContext.Current.CancellationToken);

      task.IsCompleted.Should().BeFalse();

      await cts.CancelAsync();
      var act = async () => await task;
      await act.Should().ThrowAsync<OperationCanceledException>();
   }

   private static TranslationToolsClient CreateClient(
      HttpMessageHandler handler,
      TimeProvider time,
      TimeSpan? lookupTimeout = null,
      bool throwOnLookupError = false,
      ILogger? logger = null)
   {
      return new TranslationToolsClient(
         new HttpClient(handler),
         Options.Create(new TranslationToolsClientOptions
         {
            ApiKey = "api-key",
            LookupTimeout = lookupTimeout ?? TimeSpan.FromSeconds(5),
            ThrowOnLookupError = throwOnLookupError
         }),
         new LocalTranslationToolsClientCache(),
         time,
         logger: logger
      );
   }

   private sealed class NeverCompletingHandler : HttpMessageHandler
   {
      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         await Task.Delay(Timeout.Infinite, cancellationToken);
         throw new InvalidOperationException("Unreachable: the delay above should have been cancelled.");
      }
   }

   /// <summary>
   /// Answers with headers straight away, then never produces a body. The timeout has to cover
   /// reading the body too, not only getting the headers.
   /// </summary>
   private sealed class StallingBodyHandler : HttpMessageHandler
   {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
         return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
         {
            Content = new StreamContent(new StallingStream())
         });
      }
   }

   private sealed class StallingStream : Stream
   {
      public override bool CanRead => true;
      public override bool CanSeek => false;
      public override bool CanWrite => false;
      public override long Length => throw new NotSupportedException();

      public override long Position
      {
         get => throw new NotSupportedException();
         set => throw new NotSupportedException();
      }

      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
         await Task.Delay(Timeout.Infinite, cancellationToken);
         return 0;
      }

      public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      {
         return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
      }

      public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
      public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
      public override void SetLength(long value) => throw new NotSupportedException();
      public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
      public override void Flush() { }
   }
}
