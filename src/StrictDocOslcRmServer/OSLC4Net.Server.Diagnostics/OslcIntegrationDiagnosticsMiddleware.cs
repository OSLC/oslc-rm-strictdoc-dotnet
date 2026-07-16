using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OSLC4Net.Server.Diagnostics;

/// <summary>
/// Records concise Trace-level HTTP exchange details and, when enabled, persists failed OSLC
/// request/response exchanges without placing raw RDF or HTML bodies in the application log.
/// </summary>
public sealed class OslcIntegrationDiagnosticsMiddleware(
    RequestDelegate next,
    IOptions<OslcIntegrationDiagnosticsOptions> options,
    ILogger<OslcIntegrationDiagnosticsMiddleware> logger)
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
        "X-Api-Key"
    };

    private readonly OslcIntegrationDiagnosticsOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var startedAt = DateTimeOffset.UtcNow;
        var traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;
        var configurationContext = request.Headers["Configuration-Context"].ToString();
        var userAgent = request.Headers.UserAgent.ToString();

        logger.LogTrace(
            "Incoming HTTP request {Method} {Scheme}://{Host}{PathBase}{Path}{QueryString}; " +
            "TraceIdentifier={TraceIdentifier}; TraceId={TraceId}; ConfigurationContext={ConfigurationContext}; " +
            "UserAgent={UserAgent}; Accept={Accept}; ContentType={ContentType}",
            request.Method,
            request.Scheme,
            request.Host,
            request.PathBase,
            request.Path,
            request.QueryString,
            context.TraceIdentifier,
            traceId,
            configurationContext,
            userAgent,
            request.Headers.Accept.ToString(),
            request.ContentType);

        if (!_options.CapturePayloads)
        {
            await next(context).ConfigureAwait(false);
            LogResponse(context, startedAt, traceId);
            return;
        }

        request.EnableBuffering();
        var captureDirectory = Path.GetFullPath(_options.CaptureDirectory);
        Directory.CreateDirectory(captureDirectory);
        var responseCapturePath = Path.Combine(captureDirectory, $".{Guid.NewGuid():N}.response");
        var originalResponseBody = context.Response.Body;
        Exception? requestException = null;

        await using (var responseCapture = new FileStream(
                         responseCapturePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 65_536,
                         useAsync: true))
        {
            context.Response.Body = new TeeWriteStream(originalResponseBody, responseCapture);
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                requestException = exception;
            }
            finally
            {
                await context.Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                context.Response.Body = originalResponseBody;
            }
        }

        try
        {
            LogResponse(context, startedAt, traceId);
            if (ShouldCapture(context.Response.StatusCode, requestException))
            {
                var filePrefix = CreateFilePrefix(context, startedAt);
                await WriteRequestLogAsync(
                    Path.Combine(captureDirectory, $"{filePrefix}_req.log"),
                    context,
                    startedAt,
                    traceId,
                    requestException).ConfigureAwait(false);
                await WriteResponseLogAsync(
                    Path.Combine(captureDirectory, $"{filePrefix}_resp.log"),
                    context,
                    startedAt,
                    traceId,
                    requestException,
                    responseCapturePath).ConfigureAwait(false);
                logger.LogTrace(
                    "Captured HTTP request/response payloads for {Method} {Path} with status {StatusCode} using prefix {CapturePrefix}",
                    request.Method,
                    request.Path,
                    context.Response.StatusCode,
                    filePrefix);
            }
        }
        finally
        {
            if (File.Exists(responseCapturePath))
            {
                File.Delete(responseCapturePath);
            }
        }

        if (requestException is not null)
        {
            ExceptionDispatchInfo.Capture(requestException).Throw();
        }
    }

    private bool ShouldCapture(int statusCode, Exception? requestException) =>
        requestException is not null ||
        (!IsRedirect(statusCode) &&
         (statusCode >= StatusCodes.Status400BadRequest ||
          (_options.CaptureSuccessfulResponses && statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices)));

    private void LogResponse(HttpContext context, DateTimeOffset startedAt, string traceId)
    {
        var response = context.Response;
        var elapsedMilliseconds = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        logger.LogTrace(
            "Completed HTTP response {StatusCode} for {Method} {Path}{QueryString}; " +
            "TraceIdentifier={TraceIdentifier}; TraceId={TraceId}; ElapsedMilliseconds={ElapsedMilliseconds}; Location={Location}",
            response.StatusCode,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.TraceIdentifier,
            traceId,
            elapsedMilliseconds,
            response.Headers.Location.ToString());

        if (IsRedirect(response.StatusCode))
        {
            logger.LogTrace(
                "HTTP response {StatusCode} redirects {Method} {Path} to {Location}",
                response.StatusCode,
                context.Request.Method,
                context.Request.Path,
                response.Headers.Location.ToString());
        }
    }

    private async Task WriteRequestLogAsync(
        string path,
        HttpContext context,
        DateTimeOffset startedAt,
        string traceId,
        Exception? requestException)
    {
        await using var output = CreateLogFile(path);
        await WritePreambleAsync(
            output,
            "Request",
            context,
            startedAt,
            traceId,
            requestException,
            context.Request.Headers).ConfigureAwait(false);
        await output.WriteAsync("\nBody:\n"u8.ToArray(), context.RequestAborted).ConfigureAwait(false);

        if (context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
        }

        await context.Request.Body.CopyToAsync(output, context.RequestAborted).ConfigureAwait(false);
        if (context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
        }
    }

    private async Task WriteResponseLogAsync(
        string path,
        HttpContext context,
        DateTimeOffset startedAt,
        string traceId,
        Exception? requestException,
        string responseCapturePath)
    {
        await using var output = CreateLogFile(path);
        await WritePreambleAsync(
            output,
            "Response",
            context,
            startedAt,
            traceId,
            requestException,
            context.Response.Headers).ConfigureAwait(false);
        await output.WriteAsync("\nBody:\n"u8.ToArray(), context.RequestAborted).ConfigureAwait(false);

        await using var capturedResponse = new FileStream(
            responseCapturePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65_536,
            useAsync: true);
        await capturedResponse.CopyToAsync(output, context.RequestAborted).ConfigureAwait(false);
    }

    private static FileStream CreateLogFile(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 65_536,
        useAsync: true);

    private async Task WritePreambleAsync(
        Stream output,
        string kind,
        HttpContext context,
        DateTimeOffset startedAt,
        string traceId,
        Exception? requestException,
        IHeaderDictionary headers)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync($"{kind}TimestampUtc: {startedAt:O}").ConfigureAwait(false);
        await writer.WriteLineAsync($"UnixTimeMilliseconds: {startedAt.ToUnixTimeMilliseconds()}").ConfigureAwait(false);
        await writer.WriteLineAsync($"TraceIdentifier: {context.TraceIdentifier}").ConfigureAwait(false);
        await writer.WriteLineAsync($"TraceId: {traceId}").ConfigureAwait(false);
        await writer.WriteLineAsync($"Method: {context.Request.Method}").ConfigureAwait(false);
        await writer.WriteLineAsync($"Url: {context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}").ConfigureAwait(false);
        await writer.WriteLineAsync($"StatusCode: {context.Response.StatusCode}").ConfigureAwait(false);
        if (requestException is not null)
        {
            await writer.WriteLineAsync($"Exception: {requestException.GetType().FullName}: {requestException.Message}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync("Headers:").ConfigureAwait(false);
        foreach (var header in headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var value = !_options.IncludeSensitiveHeaders && SensitiveHeaders.Contains(header.Key)
                ? "[REDACTED]"
                : header.Value.ToString();
            await writer.WriteLineAsync($"{header.Key}: {value}").ConfigureAwait(false);
        }

        await writer.FlushAsync(context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsRedirect(int statusCode) => statusCode is >= StatusCodes.Status300MultipleChoices and < StatusCodes.Status400BadRequest;

    private static string CreateFilePrefix(HttpContext context, DateTimeOffset startedAt)
    {
        var endpoint = string.Join(
            "_",
            context.Request.Method,
            context.Request.Path.Value?.Trim('/').Replace('/', '_') ?? "root");
        var safeEndpoint = string.Concat(endpoint.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_'));
        return $"{startedAt:yyyyMMddTHHmmssfffffffZ}_{startedAt.ToUnixTimeMilliseconds()}_{safeEndpoint}_{context.TraceIdentifier}";
    }

    private sealed class TeeWriteStream(Stream primary, Stream copy) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => primary.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            primary.Flush();
            copy.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await primary.FlushAsync(cancellationToken).ConfigureAwait(false);
            await copy.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            primary.Write(buffer, offset, count);
            copy.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await primary.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            await copy.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await primary.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await copy.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
