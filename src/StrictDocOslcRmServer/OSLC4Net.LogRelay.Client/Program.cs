using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

const int ExitUsage = 2;
const int ExitAuthentication = 3;
const int ExitRemote = 4;
const int ExitConfiguration = 5;
bool json = args.Contains("--json", StringComparer.Ordinal);
string[] command = args.Where(argument => argument != "--json").ToArray();

if (command.Length == 0 || command[0] is "--help" or "-h")
{
    Write(new UsageResponse(
        "log-relay <agent-context|profiles|sources|logs|feedback> [--json] [--output <file>]",
        new ExitCodes(0, ExitUsage, ExitAuthentication, ExitRemote, ExitConfiguration)),
        RelayClientJsonContext.Default.UsageResponse);
    return;
}

if (command[0] == "agent-context")
{
    Write(new ClientContext(
        "1",
        "v1",
        ["profiles list|get|set|delete", "sources list", "logs get|stream", "feedback"],
        "--url/--api-key, environment, named profile, default profile",
        "Profiles are stored locally with owner-only Unix permissions where supported."),
        RelayClientJsonContext.Default.ClientContext);
    return;
}

try
{
    Output.Configure(Option(command, "--output"));

    if (command[0] == "feedback")
    {
        Write(new FeedbackResponse("Report relay contract or client issues in the repository that ships this executable.", "feedback"), RelayClientJsonContext.Default.FeedbackResponse);
        return;
    }

    if (command[0] == "profiles")
    {
        HandleProfiles(command.Skip(1).ToArray());
        return;
    }

    RelayProfile profile = ResolveProfile(command);
    using HttpClient client = new() { BaseAddress = new Uri(profile.Url.TrimEnd('/') + "/", UriKind.Absolute) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

    if (command[0] == "sources" && command.ElementAtOrDefault(1) == "list")
    {
        await PrintResponseAsync(client, "v1/sources", json).ConfigureAwait(false);
        return;
    }

    if (command[0] == "logs" && command.ElementAtOrDefault(1) == "get")
    {
        string source = Required(command, "--source");
        int limit = ParseLimit(Option(command, "--limit"));
        string query = $"v1/logs?source={Uri.EscapeDataString(source)}&limit={limit}";
        foreach (string name in new[] { "--cursor", "--contains" })
        {
            if (Option(command, name) is { } value)
            {
                query += $"&{name[2..]}={Uri.EscapeDataString(value)}";
            }
        }
        await PrintResponseAsync(client, query, json).ConfigureAwait(false);
        return;
    }

    if (command[0] == "logs" && command.ElementAtOrDefault(1) == "stream")
    {
        string source = Required(command, "--source");
        string query = $"v1/stream?source={Uri.EscapeDataString(source)}";
        if (Option(command, "--cursor") is { } cursor)
        {
            query += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        if (Option(command, "--contains") is { } contains)
        {
            query += $"&contains={Uri.EscapeDataString(contains)}";
        }

        using HttpResponseMessage response = await client.GetAsync(query, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new RelayHttpException(response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        using StreamReader reader = new(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                Output.WriteLine(line[6..]);
            }
        }
        return;
    }

    throw new UsageException("Use `sources list`, `logs get`, or `logs stream`; run --help for syntax.");
}
catch (UsageException exception)
{
    Fail(exception.Message, ExitUsage, json);
}
catch (RelayHttpException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
{
    Fail($"Authentication failed ({(int)exception.StatusCode}). Set --api-key, LOG_RELAY_API_KEY, or a profile API key.", ExitAuthentication, json);
}
catch (RelayHttpException exception)
{
    Fail($"Relay request failed ({(int)exception.StatusCode}): {exception.Body}", ExitRemote, json);
}
catch (Exception exception) when (exception is HttpRequestException or UriFormatException)
{
    Fail($"Cannot reach the relay: {exception.Message}", ExitRemote, json);
}
catch (Exception exception)
{
    Fail(exception.Message, ExitConfiguration, json);
}

static async Task PrintResponseAsync(HttpClient client, string path, bool json)
{
    using HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false);
    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
        throw new RelayHttpException(response.StatusCode, body);
    }

    if (json)
    {
        Output.WriteLine(body);
        return;
    }

    using JsonDocument document = JsonDocument.Parse(body);
    if (document.RootElement.TryGetProperty("lines", out JsonElement lines))
    {
        foreach (JsonElement line in lines.EnumerateArray())
        {
            Output.WriteLine(line.GetProperty("text").GetString() ?? string.Empty);
        }

        return;
    }
    Output.WriteLine(JsonSerializer.Serialize(document.RootElement, RelayClientJsonContext.Default.JsonElement));
}

static void HandleProfiles(string[] args)
{
    string action = args.FirstOrDefault() ?? throw new UsageException("Use profiles list|get|set|delete.");
    Dictionary<string, RelayProfile> profiles = LoadProfiles();
    if (action == "list")
    {
        Write(new ProfilesResponse(profiles.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new ProfileSummary(pair.Key, pair.Value.Url)).ToArray()), RelayClientJsonContext.Default.ProfilesResponse);
        return;
    }

    string name = Required(args, "--name");
    if (action == "get")
    {
        if (!profiles.TryGetValue(name, out RelayProfile? profile))
        {
            throw new UsageException($"Profile '{name}' does not exist.");
        }

        Write(new ProfileDetails(name, profile.Url), RelayClientJsonContext.Default.ProfileDetails);
        return;
    }
    if (action == "set")
    {
        if (profiles.ContainsKey(name) && !args.Contains("--force", StringComparer.Ordinal))
        {
            throw new UsageException($"Profile '{name}' exists; repeat with --force to replace it.");
        }

        string url = Required(args, "--url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new UsageException("--url must be an absolute HTTP(S) URL.");
        }

        profiles[name] = new RelayProfile(url, Required(args, "--api-key"));
        SaveProfiles(profiles);
        Write(new ProfileMutation(name, true, false), RelayClientJsonContext.Default.ProfileMutation);
        return;
    }
    if (action == "delete")
    {
        if (!args.Contains("--force", StringComparer.Ordinal))
        {
            throw new UsageException("Profile deletion requires --force.");
        }

        profiles.Remove(name);
        SaveProfiles(profiles);
        Write(new ProfileMutation(name, false, true), RelayClientJsonContext.Default.ProfileMutation);
        return;
    }
    throw new UsageException("Use profiles list|get|set|delete.");
}

static RelayProfile ResolveProfile(string[] args)
{
    string? url = Option(args, "--url") ?? Environment.GetEnvironmentVariable("LOG_RELAY_URL");
    string? apiKey = Option(args, "--api-key") ?? Environment.GetEnvironmentVariable("LOG_RELAY_API_KEY");
    if (url is not null && apiKey is not null)
    {
        return new RelayProfile(url, apiKey);
    }

    string name = Option(args, "--profile") ?? Environment.GetEnvironmentVariable("LOG_RELAY_PROFILE") ?? "default";
    if (!LoadProfiles().TryGetValue(name, out RelayProfile? profile))
    {
        throw new InvalidOperationException($"No credentials supplied and profile '{name}' does not exist. Use profiles set --name {name} --url <url> --api-key <key>.");
    }

    return new RelayProfile(url ?? profile.Url, apiKey ?? profile.ApiKey);
}

static string Required(IEnumerable<string> args, string name)
{
    string? value = Option(args, name);
    return !string.IsNullOrWhiteSpace(value) ? value : throw new UsageException($"{name} is required.");
}

static string? Option(IEnumerable<string> args, string name)
{
    string[] values = args.ToArray();
    int index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal) ? values[index + 1] : null;
}

static int ParseLimit(string? value)
{
    if (value is null)
    {
        return 100;
    }

    if (!int.TryParse(value, out int parsed) || parsed is < 1 or > 1_000)
    {
        throw new UsageException("--limit must be an integer from 1 through 1000.");
    }

    return parsed;
}

static string ProfilePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "oslc-log-relay", "profiles.json");
static Dictionary<string, RelayProfile> LoadProfiles() => File.Exists(ProfilePath()) ? JsonSerializer.Deserialize(File.ReadAllText(ProfilePath()), RelayClientJsonContext.Default.DictionaryStringRelayProfile) ?? [] : [];
static void SaveProfiles(Dictionary<string, RelayProfile> profiles)
{
    string directory = Path.GetDirectoryName(ProfilePath())!;
    Directory.CreateDirectory(directory);
    File.WriteAllText(ProfilePath(), JsonSerializer.Serialize(profiles, RelayClientJsonContext.Default.DictionaryStringRelayProfile));
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(ProfilePath(), UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

static void Write<T>(T value, JsonTypeInfo<T> typeInfo) => Output.WriteLine(JsonSerializer.Serialize(value, typeInfo));
static void Fail(string message, int exitCode, bool json)
{
    ErrorResponse error = new(message, exitCode);
    if (json)
    {
        Output.WriteLine(JsonSerializer.Serialize(error, RelayClientJsonContext.Default.ErrorResponse));
    }
    else
    {
        Console.Error.WriteLine($"Error: {message}");
    }

    Environment.ExitCode = exitCode;
}

sealed record UsageResponse(string Usage, ExitCodes ExitCodes);
sealed record ExitCodes(int Success, int Usage, int Authentication, int Remote, int Configuration);
sealed record ClientContext(string Version, string ProtocolVersion, IReadOnlyList<string> Commands, string ProfilePrecedence, string CredentialStorage);
sealed record FeedbackResponse(string Feedback, string Command);
sealed record ProfilesResponse(IReadOnlyList<ProfileSummary> Profiles);
sealed record ProfileSummary(string Name, string Url);
sealed record ProfileDetails(string Name, string Url);
sealed record ProfileMutation(string Name, bool Saved, bool Deleted);
sealed record ErrorResponse(string Error, int ExitCode);
sealed record RelayProfile(string Url, string ApiKey);
sealed class UsageException(string message) : Exception(message);
sealed class RelayHttpException(HttpStatusCode statusCode, string body) : Exception(body) { public HttpStatusCode StatusCode { get; } = statusCode; public string Body { get; } = body; }

static class Output
{
    private static string? outputPath;

    public static void Configure(string? path)
    {
        outputPath = path;
        if (path is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, string.Empty);
    }

    public static void WriteLine(string value)
    {
        if (outputPath is null)
        {
            Console.WriteLine(value);
        }
        else
        {
            File.AppendAllText(outputPath, value + Environment.NewLine);
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(UsageResponse))]
[JsonSerializable(typeof(ClientContext))]
[JsonSerializable(typeof(FeedbackResponse))]
[JsonSerializable(typeof(ProfilesResponse))]
[JsonSerializable(typeof(ProfileDetails))]
[JsonSerializable(typeof(ProfileMutation))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(RelayProfile))]
[JsonSerializable(typeof(Dictionary<string, RelayProfile>))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class RelayClientJsonContext : JsonSerializerContext;
