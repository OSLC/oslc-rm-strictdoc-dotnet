using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OSLC4Net.Server.Diagnostics;

namespace StrictDocOslcRm.Tests;

public sealed class OslcIntegrationDiagnosticsMiddlewareTests : IAsyncDisposable
{
    private readonly string _captureDirectory = Path.Combine(Path.GetTempPath(), "strictdoc-oslc-rm-tests", Guid.NewGuid().ToString("N"));

    [Test]
    public async Task InvokeAsync_ForBadResponseWritesPairedPayloadFilesWithUsefulMetadata()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "jazz-request-42";
        context.Request.Method = HttpMethods.Put;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("strictdoc.example.test");
        context.Request.Path = "/oslc/service_provider/demo/requirements";
        context.Request.QueryString = new QueryString("?oslc.where=foo");
        context.Request.Headers["Configuration-Context"] = "https://strictdoc.example.test/oslc_config/configurations/main/HEAD";
        context.Request.Headers["X-Com-Ibm-Team-Trace-Identifier"] = "A250022C";
        context.Request.Headers.UserAgent = "Jazz/7.0.2";
        context.Request.Headers.Authorization = "OAuth secret";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("incoming rdf"));
        context.Response.Body = new MemoryStream();

        var middleware = CreateMiddleware(async httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.Headers["OSLC-Core-Version"] = "2.0";
            await httpContext.Response.WriteAsync("baseline already exists");
        });

        await middleware.InvokeAsync(context);

        var requestPath = Directory.GetFiles(_captureDirectory, "*_req.log").Single();
        var responsePath = Directory.GetFiles(_captureDirectory, "*_resp.log").Single();
        var requestLog = await File.ReadAllTextAsync(requestPath);
        var responseLog = await File.ReadAllTextAsync(responsePath);

        await Assert.That(requestLog).Contains("RequestTimestampUtc:");
        await Assert.That(Path.GetFileName(requestPath)).Contains("_JazzTrace_A250022C_req.log");
        await Assert.That(requestLog).Contains("UnixTimeMilliseconds:");
        await Assert.That(requestLog).Contains("TraceIdentifier: jazz-request-42");
        await Assert.That(requestLog).Contains("Configuration-Context: https://strictdoc.example.test/oslc_config/configurations/main/HEAD");
        await Assert.That(requestLog).Contains("Authorization: [REDACTED]");
        await Assert.That(requestLog).Contains("incoming rdf");
        await Assert.That(responseLog).Contains("StatusCode: 409");
        await Assert.That(responseLog).Contains("OSLC-Core-Version: 2.0");
        await Assert.That(responseLog).Contains("baseline already exists");
    }

    [Test]
    public async Task InvokeAsync_DoesNotCaptureRedirectsOrSuccessfulResponsesByDefault()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/redirect";
        context.Response.Body = new MemoryStream();

        var middleware = CreateMiddleware(httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status302Found;
            httpContext.Response.Headers.Location = "/target";
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        await Assert.That(Directory.GetFiles(_captureDirectory, "*.log")).IsEmpty();
    }

    [Test]
    public async Task InvokeAsync_LogsOslcOrNonHtmlRequestsButSuppressesBrowserNavigation()
    {
        var logger = new RecordingLogger<OslcIntegrationDiagnosticsMiddleware>();
        var middleware = CreateMiddleware(
            httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            logger: logger);

        var browserContext = new DefaultHttpContext();
        browserContext.Request.Headers.Accept = "text/html,application/xhtml+xml";
        await middleware.InvokeAsync(browserContext);
        await Assert.That(logger.Messages).IsEmpty();

        var oslcContext = new DefaultHttpContext();
        oslcContext.Request.Headers.Accept = "text/html";
        oslcContext.Request.Headers["OSLC-Core-Version"] = "2.0";
        await middleware.InvokeAsync(oslcContext);
        await Assert.That(logger.Messages.Count).IsEqualTo(2);

        logger.Messages.Clear();
        var rdfContext = new DefaultHttpContext();
        rdfContext.Request.Headers.Accept = "application/rdf+xml";
        await middleware.InvokeAsync(rdfContext);
        await Assert.That(logger.Messages.Count).IsEqualTo(2);
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_captureDirectory))
        {
            await Task.Run(() => Directory.Delete(_captureDirectory, recursive: true));
        }
    }

    private OslcIntegrationDiagnosticsMiddleware CreateMiddleware(
        RequestDelegate next,
        ILogger<OslcIntegrationDiagnosticsMiddleware>? logger = null) => new(
            next,
            Options.Create(new OslcIntegrationDiagnosticsOptions
            {
                CapturePayloads = true,
                CaptureDirectory = _captureDirectory
            }),
            logger ?? NullLogger<OslcIntegrationDiagnosticsMiddleware>.Instance);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
