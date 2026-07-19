using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

const string ProtocolVersion = "v1";
const int DefaultLimit = 100;
const int MaximumLimit = 1_000;
const string Html = """
<!doctype html><meta charset="utf-8"><title>Log relay</title><style>body{font:14px ui-monospace,monospace;margin:2rem}input,button{font:inherit}pre{white-space:pre-wrap;background:#111;color:#ddd;padding:1rem;max-height:75vh;overflow:auto}</style>
<h1>Log relay</h1><p>Enter a temporary API key; it is exchanged for an HttpOnly session cookie.</p><input id="key" type="password" placeholder="API key"><button id="connect">Connect</button><select id="source"></select><pre id="output"></pre>
<script>
const key=document.querySelector('#key'),source=document.querySelector('#source'),output=document.querySelector('#output');let stream;
async function api(path,init={}){return fetch(path,{...init,headers:{Authorization:`Bearer ${key.value}`,...init.headers}})}
document.querySelector('#connect').onclick=async()=>{let r=await api('/v1/session',{method:'POST'});if(!r.ok){output.textContent='Authentication failed';return}r=await api('/v1/sources');const data=await r.json();source.replaceChildren(...data.sources.map(x=>new Option(x.name,x.name)));source.onchange=start;start()};
function start(){stream?.close();output.textContent='';stream=new EventSource(`/v1/stream?source=${encodeURIComponent(source.value)}`);stream.addEventListener('line',event=>{const line=JSON.parse(event.data);output.textContent+=line.text+'\\n';output.scrollTop=output.scrollHeight})}
</script>
""";

if (args is ["--help" or "-h"])
{
    Console.WriteLine("OSLC4Net.Server.LogRelay\n\nServe named, read-only diagnostic files or directories.\nConfiguration: LOG_RELAY_API_KEY and LOG_RELAY_SOURCES (JSON object mapping names to paths).\nCommands: agent-context, --version");
    return;
}

if (args is ["--version"])
{
    Console.WriteLine("oslc4net-log-relay 0.1.0");
    return;
}

if (args is ["agent-context", ..])
{
    Console.WriteLine(JsonSerializer.Serialize(
        new ExecutableContext("1", ProtocolVersion, "oslc4net-log-relay", ["/v1/agent-context", "/v1/sources", "/v1/logs", "/v1/stream"], "Bearer API key or relay_session cookie", false),
        RelayJsonContext.Default.ExecutableContext));
    return;
}

RelayOptions options;
try
{
    options = RelayOptions.FromEnvironment();
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine($"Configuration error: {exception.Message}");
    Environment.ExitCode = 5;
    return;
}

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(options.Urls);
WebApplication app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/index.html")
    {
        await next(context).ConfigureAwait(false);
        return;
    }

    if (!IsAuthorized(context, options.ApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ErrorResponse("authentication_required", "Supply Authorization: Bearer <API key>."), RelayJsonContext.Default.ErrorResponse).ConfigureAwait(false);
        return;
    }

    await next(context).ConfigureAwait(false);
});

app.MapGet("/", () => Results.Content(Html, "text/html; charset=utf-8"));
app.MapGet("/v1/agent-context", () => Json(new RelayContext("1", ProtocolVersion, "oslc4net", ["file", "directory"], MaximumLimit, true, false), RelayJsonContext.Default.RelayContext));
app.MapGet("/v1/sources", () => Json(new SourcesResponse(ProtocolVersion, options.Sources.Keys.OrderBy(name => name, StringComparer.Ordinal).Select(name => new SourceDescriptor(name, SourceKind(options.Sources[name]))).ToArray()), RelayJsonContext.Default.SourcesResponse));
app.MapPost("/v1/session", (HttpContext context) =>
{
    context.Response.Cookies.Append("relay_session", options.ApiKey, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = options.SecureCookies,
        MaxAge = TimeSpan.FromHours(4)
    });
    return Results.NoContent();
});
app.MapGet("/v1/logs", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    if (!TryGetSource(request, options, out string source, out string path, out IResult? error))
    {
        return error!;
    }

    int limit = ParseLimit(request.Query["limit"]);
    LogPage page = await ReadPageAsync(path, source, request.Query["cursor"].FirstOrDefault(), request.Query["contains"].FirstOrDefault(), limit, cancellationToken).ConfigureAwait(false);
    return Json(page, RelayJsonContext.Default.LogPage);
});
app.MapGet("/v1/stream", async (HttpContext context, CancellationToken cancellationToken) =>
{
    if (!TryGetSource(context.Request, options, out string source, out string path, out IResult? error))
    {
        await error!.ExecuteAsync(context).ConfigureAwait(false);
        return;
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";
    string? contains = context.Request.Query["contains"].FirstOrDefault();
    long lastIndex = long.TryParse(context.Request.Query["cursor"].FirstOrDefault(), out long parsed)
        ? parsed
        : Math.Max(0, CountLines(path) - DefaultLimit);

    while (!cancellationToken.IsCancellationRequested)
    {
        LogPage page = await ReadPageAsync(path, source, lastIndex.ToString(), contains, MaximumLimit, cancellationToken).ConfigureAwait(false);
        foreach (LogLine line in page.Lines)
        {
            string payload = JsonSerializer.Serialize(line, RelayJsonContext.Default.LogLine);
            await context.Response.WriteAsync($"event: line\ndata: {payload}\n\n", cancellationToken).ConfigureAwait(false);
        }

        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        lastIndex = long.TryParse(page.NextCursor, out long next) ? next : lastIndex;
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
    }
});

await app.RunAsync().ConfigureAwait(false);

static IResult Json<T>(T value, JsonTypeInfo<T> typeInfo) => Results.Json(value, typeInfo);

static bool TryGetSource(HttpRequest request, RelayOptions options, out string source, out string path, out IResult? error)
{
    source = request.Query["source"].FirstOrDefault() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(source))
    {
        path = string.Empty;
        error = Json(new ErrorResponse("source_required", "Use a source returned by GET /v1/sources."), RelayJsonContext.Default.ErrorResponse);
        return false;
    }

    if (!options.Sources.TryGetValue(source, out string? configuredPath))
    {
        path = string.Empty;
        error = Json(new ErrorResponse("unknown_source", null, options.Sources.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray()), RelayJsonContext.Default.ErrorResponse);
        return false;
    }

    path = configuredPath;
    error = null;
    return true;
}

static bool IsAuthorized(HttpContext context, string apiKey)
{
    string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
    string? candidate = authorization is not null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authorization["Bearer ".Length..]
        : context.Request.Cookies["relay_session"];
    return candidate is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(apiKey));
}

static int ParseLimit(string? raw) => int.TryParse(raw, out int parsed) ? Math.Clamp(parsed, 1, MaximumLimit) : DefaultLimit;

static async Task<LogPage> ReadPageAsync(string path, string source, string? cursor, string? contains, int limit, CancellationToken cancellationToken)
{
    long start = long.TryParse(cursor, out long parsed) && parsed >= 0 ? parsed : -1;
    long total = 0;
    Queue<LogLine> tail = new(limit);
    List<LogLine> page = [];
    foreach (string text in ReadSourceLines(path))
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool matches = string.IsNullOrEmpty(contains) || text.Contains(contains, StringComparison.Ordinal);
        if (start >= 0 && total >= start && matches && page.Count < limit)
        {
            page.Add(new LogLine((total + 1).ToString(), text));
        }
        else if (start < 0 && matches)
        {
            if (tail.Count == limit)
            {
                tail.Dequeue();
            }
            tail.Enqueue(new LogLine((total + 1).ToString(), text));
        }
        total++;
    }

    IReadOnlyList<LogLine> lines = start < 0 ? tail.ToArray() : page;
    bool truncated = start >= 0 && total > start + page.Count;
    await Task.CompletedTask.ConfigureAwait(false);
    return new LogPage(ProtocolVersion, source, lines, total.ToString(), truncated, false);
}

static IEnumerable<string> ReadSourceLines(string path)
{
    if (File.Exists(path))
    {
        return File.ReadLines(path);
    }

    if (!Directory.Exists(path))
    {
        return [];
    }

    return Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
        .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
        .SelectMany(file => new[] { $"--- {Path.GetFileName(file)} ---" }.Concat(File.ReadLines(file)));
}

static long CountLines(string path) => ReadSourceLines(path).LongCount();
static string SourceKind(string path) => Directory.Exists(path) ? "directory" : "file";

sealed record ExecutableContext(string Version, string ProtocolVersion, string Executable, IReadOnlyList<string> Endpoints, string Authentication, bool Mutations);
sealed record RelayContext(string Version, string ProtocolVersion, string Relay, IReadOnlyList<string> SourceKinds, int MaximumLimit, bool SupportsSse, bool Mutations);
sealed record SourcesResponse(string ProtocolVersion, IReadOnlyList<SourceDescriptor> Sources);
sealed record SourceDescriptor(string Name, string Kind);
sealed record ErrorResponse(string Error, string? Recovery = null, IReadOnlyList<string>? ValidSources = null);
sealed record LogLine(string Cursor, string Text);
sealed record LogPage(string ProtocolVersion, string Source, IReadOnlyList<LogLine> Lines, string NextCursor, bool Truncated, bool Reset);

sealed record RelayOptions(string ApiKey, IReadOnlyDictionary<string, string> Sources, string Urls, bool SecureCookies)
{
    public static RelayOptions FromEnvironment()
    {
        string apiKey = Environment.GetEnvironmentVariable("LOG_RELAY_API_KEY") ?? throw new InvalidOperationException("LOG_RELAY_API_KEY is required.");
        string rawSources = Environment.GetEnvironmentVariable("LOG_RELAY_SOURCES") ?? throw new InvalidOperationException("LOG_RELAY_SOURCES is required as a JSON object mapping source names to files or directories.");
        Dictionary<string, string>? sources = JsonSerializer.Deserialize(rawSources, RelayJsonContext.Default.DictionaryStringString);
        if (sources is not { Count: > 0 } || sources.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new InvalidOperationException("LOG_RELAY_SOURCES must contain one or more non-empty names and paths.");
        }

        return new RelayOptions(apiKey, sources.ToDictionary(pair => pair.Key, pair => Path.GetFullPath(pair.Value), StringComparer.Ordinal), Environment.GetEnvironmentVariable("LOG_RELAY_URLS") ?? "http://127.0.0.1:8742", !string.Equals(Environment.GetEnvironmentVariable("LOG_RELAY_SECURE_COOKIES"), "false", StringComparison.OrdinalIgnoreCase));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ExecutableContext))]
[JsonSerializable(typeof(RelayContext))]
[JsonSerializable(typeof(SourcesResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(LogLine))]
[JsonSerializable(typeof(LogPage))]
internal sealed partial class RelayJsonContext : JsonSerializerContext;
