using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;

public static class ToyOAuth1AspNetExtensions
{
    // REVISIT: Replace this toy OAuth1 implementation with a production-grade OAuth
    // provider/consumer registry with durable storage, admin approval workflow,
    // nonce expiry, key rotation, and audited token revocation.
    public static IServiceCollection AddToyOAuth1(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders =
                ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
        });

        var storePath = configuration["OAuth1:StorePath"]
                        ?? Environment.GetEnvironmentVariable("OAUTH1_STORE")
                        ?? "oauth-store.json";
        var publicBaseUrl = Environment.GetEnvironmentVariable("PUBLIC_BASE_URL")
                            ?? configuration["OSLC:PublicBaseUrl"]
                            ?? configuration["OSLC:PublicBaseUri"]
                            ?? "";
        var seedClients = GetSeedClients(configuration);

        services.AddSingleton(new OAuthStore(storePath, seedClients));
        services.AddSingleton(new OAuth1(publicBaseUrl));
        return services;
    }

    private static IReadOnlyList<Client> GetSeedClients(IConfiguration configuration)
    {
        return configuration.GetSection("OAuth1:SeedClients")
            .GetChildren()
            .Select(section =>
            {
                var key = section["Key"];
                var secret = section["Secret"];
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                {
                    return null;
                }

                return new Client
                {
                    Key = key,
                    Secret = secret,
                    Name = section["Name"] ?? key,
                    Approved = bool.TryParse(section["Approved"], out var approved)
                        ? approved
                        : true
                };
            })
            .OfType<Client>()
            .ToArray();
    }

    public static IEndpointRouteBuilder MapToyOAuth1Provider(this IEndpointRouteBuilder endpoints,
        string prefix = "/oauth")
    {
        prefix = NormalizePrefix(prefix);
        var authorizePath = prefix + "/authorize";

        endpoints.MapPost(prefix + "/request_token", RequestTokenAsync);
        endpoints.MapGet(authorizePath,
            (HttpRequest req, OAuthStore store) => AuthorizePage(req, store, authorizePath));
        endpoints.MapPost(authorizePath, AuthorizeDecisionAsync);
        endpoints.MapPost(prefix + "/access_token", AccessTokenAsync);

        // An error occurred while requesting an OAuth consumer key from server
        // "https://strictdoc-rm.oslc.ldsw.eu/.well-known/oslc/rootservices.xml".ID CRJAZ1578E
        // The remote server responded to the request to generate a provisional OAuth consumer key
        // with a HTTP status code 404 error.ID CRJAZ1341E
        endpoints.MapPost(prefix + "/request_consumer_key", RequestConsumerKeyAsync);
        endpoints.MapGet(prefix + "/approve_consumer_key", ApproveConsumerKeyPage);
        endpoints.MapPost(prefix + "/approve_consumer_key", ApproveConsumerKeyDecisionAsync);

        return endpoints;
    }

    private static string NormalizePrefix(string prefix)
    {
        prefix = "/" + prefix.Trim('/');
        return string.Equals(prefix, "/", StringComparison.Ordinal) ? "" : prefix;
    }

    private static async Task<IResult> RequestTokenAsync(HttpRequest req, OAuthStore store,
        OAuth1 oauth)
    {
        var result = await oauth
            .ValidateAsync(req, store, requireToken: false, requireAccessToken: false)
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return OAuth1.Error(result.Error!);
        }

        var callback = OAuth1.LastParam(result.Parameters, "oauth_callback", "oob");
        var token = OAuth1.RandomToken();
        var secret = OAuth1.RandomToken();

        store.Upsert(s =>
        {
            s.RequestTokens[token] = new RequestToken
            {
                Token = token,
                Secret = secret,
                ConsumerKey = result.Consumer!.Key,
                Callback = callback,
                Authorized = false
            };
        });

        return Results.Text(OAuth1.FormEncode(new Dictionary<string, string>
            (StringComparer.Ordinal)
        {
            ["oauth_token"] = token,
            ["oauth_token_secret"] = secret,
            ["oauth_callback_confirmed"] = "true"
        }), "application/x-www-form-urlencoded");
    }

    private static IResult AuthorizePage(HttpRequest req, OAuthStore store, string authorizePath)
    {
        var token = req.Query["oauth_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest("missing oauth_token");
        }

        lock (store.Gate)
        {
            if (!store.Data.RequestTokens.TryGetValue(token, out var rt))
            {
                return Results.BadRequest("unknown oauth_token");
            }

            var client = store.Data.Clients.FirstOrDefault(c =>
                string.Equals(c.Key, rt.ConsumerKey, StringComparison.Ordinal));
            return Results.Content(RenderApprovalPage(authorizePath, token, rt, client),
                "text/html");
        }
    }

    private static async Task<IResult> AuthorizeDecisionAsync(HttpRequest req, OAuthStore store)
    {
        if (!req.HasFormContentType)
        {
            return Results.BadRequest("expected application/x-www-form-urlencoded");
        }

        var form = await req.ReadFormAsync().ConfigureAwait(false);
        var token = form["oauth_token"].ToString();
        var decision = form["decision"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest("missing oauth_token");
        }

        if (!string.Equals(decision, "approve", StringComparison.Ordinal))
        {
            return CancelRequestToken(store, token);
        }

        var verifier = OAuth1.RandomVerifier();
        string callback;
        lock (store.Gate)
        {
            if (!store.Data.RequestTokens.TryGetValue(token, out var rt))
            {
                return Results.BadRequest("unknown oauth_token");
            }

            rt.Authorized = true;
            rt.Verifier = verifier;
            callback = rt.Callback;
            store.SaveUnsafe();
        }

        if (!string.IsNullOrWhiteSpace(callback) &&
            !string.Equals(callback, "oob", StringComparison.Ordinal))
        {
            return RedirectWithOAuthParams(callback, new Dictionary<string, string>
                (StringComparer.Ordinal)
            {
                ["oauth_token"] = token,
                ["oauth_verifier"] = verifier
            });
        }

        return Results.Content(RenderApprovedPage(token, verifier), "text/html");
    }

    private static IResult CancelRequestToken(OAuthStore store, string token)
    {
        string callback;
        lock (store.Gate)
        {
            if (!store.Data.RequestTokens.TryGetValue(token, out var rt))
            {
                return Results.BadRequest("unknown oauth_token");
            }

            callback = rt.Callback;
            store.Data.RequestTokens.Remove(token);
            store.SaveUnsafe();
        }

        if (!string.IsNullOrWhiteSpace(callback) &&
            !string.Equals(callback, "oob", StringComparison.Ordinal))
        {
            return RedirectWithOAuthParams(callback, new Dictionary<string, string>
                (StringComparer.Ordinal)
            {
                ["oauth_token"] = token,
                ["oauth_problem"] = "permission_denied"
            });
        }

        return Results.Content(RenderCanceledPage(token), "text/html");
    }

    private static async Task<IResult> AccessTokenAsync(HttpRequest req, OAuthStore store,
        OAuth1 oauth)
    {
        var result = await oauth
            .ValidateAsync(req, store, requireToken: true, requireAccessToken: false)
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return OAuth1.Error(result.Error!);
        }

        var verifier = OAuth1.LastParam(result.Parameters, "oauth_verifier", "");
        var requestToken = result.RequestToken!;
        if (!requestToken.Authorized ||
            !string.Equals(requestToken.Verifier, verifier, StringComparison.Ordinal))
        {
            return OAuth1.Error("bad_verifier", StatusCodes.Status401Unauthorized);
        }

        var accessToken = OAuth1.RandomToken();
        var accessSecret = OAuth1.RandomToken();

        store.Upsert(s =>
        {
            s.AccessTokens[accessToken] = new AccessToken
            {
                Token = accessToken,
                Secret = accessSecret,
                ConsumerKey = requestToken.ConsumerKey,
                User = "toy-user"
            };
            s.RequestTokens.Remove(requestToken.Token);
        });

        return Results.Text(OAuth1.FormEncode(new Dictionary<string, string>
            (StringComparer.Ordinal)
        {
            ["oauth_token"] = accessToken,
            ["oauth_token_secret"] = accessSecret
        }), "application/x-www-form-urlencoded");
    }

    private static IResult RedirectWithOAuthParams(string callback,
        Dictionary<string, string> values)
    {
        var sep = callback.Contains('?') ? '&' : '?';
        var query = OAuth1.FormEncode(values);
        return Results.Redirect($"{callback}{sep}{query}");
    }

    private static string RenderApprovalPage(string action, string token, RequestToken rt,
        Client? client)
    {
        var clientName = string.IsNullOrWhiteSpace(client?.Name) ? rt.ConsumerKey : client!.Name;
        return $$"""
                 <!doctype html>
                 <html lang="en">
                 <head>
                   <meta charset="utf-8">
                   <title>Approve OAuth access</title>
                   <style>
                     body {margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f6f7f9; color: #1f2937; font-family: system-ui, -apple-system, Segoe UI, sans-serif; }
                     main {width: min(92vw, 36rem); background: white; border: 1px solid #d7dce2; border-radius: 14px; padding: 1.5rem; box-shadow: 0 12px 30px rgb(15 23 42 / 10%); }
                     h1 {margin: 0 0 .75rem; font-size: 1.35rem; }
                     dl {display: grid; grid-template-columns: 8rem 1fr; gap: .45rem .8rem; margin: 1rem 0; }
                     dt {color: #64748b; }
                     code {overflow - wrap: anywhere; }
                     .buttons {display: flex; gap: .75rem; margin-top: 1.25rem; }
                     button {border: 0; border-radius: 9px; padding: .65rem 1rem; cursor: pointer; font-weight: 650; }
                     button[name=decision][value=approve] {background: #111827; color: white; }
                     button[name=decision][value=cancel] {background: #e5e7eb; color: #111827; }
                   </style>
                 </head>
                 <body>
                   <main>
                     <h1>Approve OAuth access?</h1>
                     <p>A client is asking this toy OAuth provider for an access token.</p>
                     <dl>
                       <dt>Application</dt><dd>{{H(clientName)}}</dd>
                       <dt>Consumer key</dt><dd><code>{{H(rt.ConsumerKey)}}</code></dd>
                       <dt>Request token</dt><dd><code>{{H(token)}}</code></dd>
                       <dt>Callback</dt><dd><code>{{H(rt.Callback)}}</code></dd>
                     </dl>
                     <form method="post" action="{{H(action)}}">
                       <input type="hidden" name="oauth_token" value="{{H(token)}}">
                       <div class="buttons">
                         <button type="submit" name="decision" value="approve">Approve</button>
                         <button type="submit" name="decision" value="cancel">Cancel</button>
                       </div>
                     </form>
                   </main>
                 </body>
                 </html>
                 """;
    }

    private static string RenderApprovedPage(string token, string verifier) => $"""
         <!doctype html>
         <html lang="en"><body style="font-family: system-ui, sans-serif; padding: 2rem">
           <h1>OAuth token approved</h1>
           <p>oauth_token: <code>{H(token)}</code></p>
           <p>oauth_verifier: <code>{H(verifier)}</code></p>
         </body></html>
         """;

    private static string RenderCanceledPage(string token) => $"""
                                                               <!doctype html>
                                                               <html lang="en"><body style="font-family: system-ui, sans-serif; padding: 2rem">
                                                                 <h1>OAuth request canceled</h1>
                                                                 <p>The request token was rejected and removed.</p>
                                                                 <p>oauth_token: <code>{H(token)}</code></p>
                                                               </body></html>
                                                               """;

    private static string H(string value) => System.Net.WebUtility.HtmlEncode(value);

    private sealed record ConsumerKeyRequest(
        string? Name,
        string? Secret,
        string? SecretType,
        bool? Trusted,
        string? UserId,
        string? ConsumerKey);

    private static async Task<IResult> RequestConsumerKeyAsync(HttpRequest req, OAuthStore store)
    {
        var input = await ReadConsumerKeyRequestAsync(req);

        var key = string.IsNullOrWhiteSpace(input.ConsumerKey)
            ? OAuth1.RandomToken()
            : input.ConsumerKey.Trim();

        var secret = string.IsNullOrWhiteSpace(input.Secret)
            ? "jazzsecret"
            : input.Secret.Trim();

        var name = string.IsNullOrWhiteSpace(input.Name)
            ? "IBM Jazz"
            : input.Name.Trim();

        store.Upsert(s =>
        {
            var client = s.Clients.FirstOrDefault(c => c.Key == key);

            if (client is null)
            {
                s.Clients.Add(new Client
                {
                    Key = key,
                    Secret = secret,
                    Name = name,
                    Approved = false
                });
                return;
            }

            // Idempotent: same key+secret keeps existing approval state.
            // Changed secret means new trust decision required.
            if (!string.Equals(client.Secret, secret, StringComparison.Ordinal))
            {
                client.Secret = secret;
                client.Approved = false;
            }

            client.Name = name;
        });

        return Results.Json(new { key }, contentType: "text/json");
    }

    private static IResult ApproveConsumerKeyPage(HttpRequest req, OAuthStore store)
    {
        var key = req.Query["oauth_consumer_key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "jazz";
        }

        lock (store.Gate)
        {
            var client = store.Data.Clients.FirstOrDefault(c => c.Key == key);
            if (client is null)
            {
                return Results.BadRequest("unknown oauth_consumer_key");
            }

            return Results.Content($$"""
                                    <!doctype html>
                                    <html lang="en">
                                    <head>
                                      <meta charset="utf-8">
                                      <title>Approve OAuth consumer key</title>
                                      <style>
                                        body {margin:0; min-height:100vh; display:grid; place-items:center; background:#f6f7f9; font-family:system-ui,sans-serif; }
                                        main {width:min(92vw,34rem); background:white; border:1px solid #ddd; border-radius:14px; padding:1.5rem; box-shadow:0 12px 30px rgb(15 23 42 / 10%); }
                                        code {overflow - wrap:anywhere; }
                                        .buttons {display:flex; gap:.75rem; margin-top:1.25rem; }
                                        button {border:0; border-radius:9px; padding:.65rem 1rem; font-weight:650; cursor:pointer; }
                                        .approve {background:#111827; color:white; }
                                        .cancel {background:#e5e7eb; color:#111827; }
                                      </style>
                                    </head>
                                    <body>
                                      <main>
                                        <h1>Approve OAuth consumer key?</h1>
                                        <p>This toy provider will allow the client to request OAuth tokens.</p>
                                        <p><b>Name:</b> {{H(client.Name)}}</p>
                                        <p><b>Consumer key:</b> <code>{{H(client.Key)}}</code></p>
                                        <p><b>Status:</b> {{(client.Approved ? "already approved" : "pending")}}</p>
                                        <form method="post">
                                          <input type="hidden" name="oauth_consumer_key" value="{{H(client.Key)}}">
                                          <div class="buttons">
                                            <button class="approve" name="decision" value="approve">Approve</button>
                                            <button class="cancel" name="decision" value="cancel">Cancel</button>
                                          </div>
                                        </form>
                                      </main>
                                    </body>
                                    </html>
                                    """, "text/html");
        }
    }

    private static async Task<IResult> ApproveConsumerKeyDecisionAsync(HttpRequest req,
        OAuthStore store)
    {
        var form = await req.ReadFormAsync();
        var key = form["oauth_consumer_key"].ToString();
        var decision = form["decision"].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Results.BadRequest("missing oauth_consumer_key");
        }

        store.Upsert(s =>
        {
            var client = s.Clients.FirstOrDefault(c => c.Key == key);
            if (client is null)
            {
                return;
            }

            if (decision == "approve")
            {
                client.Approved = true;
            }
            else if (!client.Approved)
            {
                s.Clients.Remove(client);
            }
        });

        return Results.Content($"""
                                <!doctype html>
                                <html lang="en">
                                <body style="font-family: system-ui, sans-serif; padding: 2rem">
                                  <h1>{(decision == "approve" ? "Consumer key approved" : "Consumer key canceled")}</h1>
                                  <p><code>{H(key)}</code></p>
                                </body>
                                </html>
                                """, "text/html");
    }

    private static async Task<ConsumerKeyRequest> ReadConsumerKeyRequestAsync(HttpRequest req)
    {
        if (req.HasFormContentType)
        {
            var form = await req.ReadFormAsync();
            return new ConsumerKeyRequest(
                form["name"],
                form["secret"],
                form["secretType"],
                bool.TryParse(form["trusted"].ToString(), out var trusted) ? trusted : null,
                form["userId"],
                form["oauth_consumer_key"]);
        }

        if (req.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await JsonSerializer.DeserializeAsync<ConsumerKeyRequest>(
                       req.Body,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                   new(null, null, null, null, null, null);
        }

        return new(null, null, null, null, null, null);
    }
}

public sealed class OAuth1(string configuredPublicBaseUrl)
{
    private readonly string _publicBaseUrl = configuredPublicBaseUrl.TrimEnd('/');
    private static readonly TimeSpan TimestampSkew = TimeSpan.FromMinutes(5);

    public string PublicBaseUrlOrRequest(HttpRequest req)
    {
        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return _publicBaseUrl;
        }

        return $"{req.Scheme}://{req.Host}";
    }

    public async Task<ValidationResult> ValidateAsync(HttpRequest req, OAuthStore store,
        bool requireToken, bool requireAccessToken)
    {
        var allParams = await CollectParametersAsync(req).ConfigureAwait(false);
        var oauth = allParams.Where(p => p.Key.StartsWith("oauth_", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value.LastOrDefault() ?? "", StringComparer.Ordinal);

        string Required(string key) => oauth.TryGetValue(key, out var value) ? value : "";
        var consumerKey = Required("oauth_consumer_key");
        var signature = Required("oauth_signature");
        var signatureMethod = Required("oauth_signature_method");
        var timestamp = Required("oauth_timestamp");
        var nonce = Required("oauth_nonce");
        var tokenValue = Required("oauth_token");

        if (consumerKey.Length == 0 || signature.Length == 0 || signatureMethod.Length == 0 ||
            timestamp.Length == 0 || nonce.Length == 0)
        {
            return ValidationResult.Fail("missing_oauth_parameter", allParams);
        }

        if (!string.Equals(signatureMethod, "HMAC-SHA1", StringComparison.Ordinal))
        {
            return ValidationResult.Fail("unsupported_signature_method", allParams);
        }

        if (!long.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture,
                out var ts))
        {
            return ValidationResult.Fail("bad_timestamp", allParams);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > TimestampSkew.TotalSeconds)
        {
            return ValidationResult.Fail("stale_timestamp", allParams);
        }

        Client? consumer;
        RequestToken? requestToken = null;
        AccessToken? accessToken = null;
        string tokenSecret = "";

        lock (store.Gate)
        {
            consumer = store.Data.Clients.FirstOrDefault(c =>
                string.Equals(c.Key, consumerKey, StringComparison.Ordinal));
            if (consumer is null)
            {
                return ValidationResult.Fail("unknown_consumer", allParams);
            }

            if (requireToken)
            {
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    return ValidationResult.Fail("missing_oauth_token", allParams);
                }

                if (requireAccessToken)
                {
                    if (!store.Data.AccessTokens.TryGetValue(tokenValue, out accessToken))
                    {
                        return ValidationResult.Fail("unknown_access_token", allParams);
                    }

                    if (!string.Equals(accessToken.ConsumerKey, consumerKey,
                            StringComparison.Ordinal))
                    {
                        return ValidationResult.Fail("token_consumer_mismatch", allParams);
                    }

                    tokenSecret = accessToken.Secret;
                }
                else
                {
                    if (!store.Data.RequestTokens.TryGetValue(tokenValue, out requestToken))
                    {
                        return ValidationResult.Fail("unknown_request_token", allParams);
                    }

                    if (!string.Equals(requestToken.ConsumerKey, consumerKey,
                            StringComparison.Ordinal))
                    {
                        return ValidationResult.Fail("token_consumer_mismatch", allParams);
                    }

                    tokenSecret = requestToken.Secret;
                }
            }

            var nonceKey = $"{consumerKey}:{tokenValue}:{timestamp}:{nonce}";
            store.Data.Nonces.RemoveAll(n => now - n.Timestamp > TimestampSkew.TotalSeconds);
            if (store.Data.Nonces.Any(n =>
                    string.Equals(n.Key, nonceKey, StringComparison.Ordinal)))
            {
                return ValidationResult.Fail("nonce_replay", allParams);
            }

            store.Data.Nonces.Add(new NonceSeen { Key = nonceKey, Timestamp = now });
            store.SaveUnsafe();
        }

        var baseString = SignatureBaseString(req, allParams);
        var expected = HmacSha1(baseString, consumer.Secret, tokenSecret);
        if (!FixedEquals(expected, signature))
        {
            return ValidationResult.Fail("invalid_signature", allParams, consumer, requestToken,
                accessToken, baseString);
        }

        return ValidationResult.Success(allParams, consumer, requestToken, accessToken, baseString);
    }

    private string SignatureBaseString(HttpRequest req, Dictionary<string, List<string>> allParams)
    {
        var baseUrl = PublicBaseUrlOrRequest(req).TrimEnd('/') + req.Path.ToString();
        if (string.IsNullOrEmpty(req.Path))
        {
            baseUrl += "/";
        }

        var pairs = new List<(string Key, string Value)>();
        foreach (var (k, values) in allParams)
        {
            if (string.Equals(k, "oauth_signature", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var v in values)
            {
                pairs.Add((k, v));
            }
        }

        var normalized = string.Join("&", pairs
            .OrderBy(p => PercentEncode(p.Key), StringComparer.Ordinal)
            .ThenBy(p => PercentEncode(p.Value), StringComparer.Ordinal)
            .Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

        return
            $"{req.Method.ToUpperInvariant()}&{PercentEncode(baseUrl)}&{PercentEncode(normalized)}";
    }

    private static async Task<Dictionary<string, List<string>>> CollectParametersAsync(
        HttpRequest req)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string k, string? v)
        {
            if (!result.TryGetValue(k, out var list))
            {
                result[k] = list = [];
            }

            list.Add(v ?? "");
        }

        foreach (var (k, vs) in req.Query)
        {
            foreach (var v in vs)
            {
                Add(k, v);
            }
        }

        var authorization = req.Headers["Authorization"].ToString();
        if (authorization.StartsWith("OAuth ", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (k, v) in ParseAuthorization(authorization))
            {
                Add(k, v);
            }
        }

        if (req.HasFormContentType)
        {
            var form = await req.ReadFormAsync().ConfigureAwait(false);
            foreach (var (k, vs) in form)
            {
                foreach (var v in vs)
                {
                    Add(k, v);
                }
            }
        }

        return result;
    }

    private static Dictionary<string, string> ParseAuthorization(string header)
    {
        var value = header["OAuth".Length..].Trim();
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in SplitCommaAware(value))
        {
            var i = part.IndexOf('=');
            if (i <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..i].Trim());
            var raw = part[(i + 1)..].Trim();
            if (raw.StartsWith('"') && raw.EndsWith('"'))
            {
                raw = raw[1..^1];
            }

            if (!string.Equals(key, "realm", StringComparison.Ordinal))
            {
                dict[key] = Uri.UnescapeDataString(raw.Replace("+", "%2B"));
            }
        }

        return dict;
    }

    private static IEnumerable<string> SplitCommaAware(string s)
    {
        var sb = new StringBuilder();
        var quoted = false;
        foreach (var ch in s)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }

            if (ch == ',' && !quoted)
            {
                yield return sb.ToString().Trim();
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString().Trim();
        }
    }

    public static string PercentEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c is '-' or '.' or '_' or '~')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    public static string FormEncode(Dictionary<string, string> values) => string.Join("&",
        values.Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

    public static string LastParam(Dictionary<string, List<string>> parameters, string key,
        string fallback = "") =>
        parameters.TryGetValue(key, out var values) && values.Count > 0 ? values[^1] : fallback;

    private static string HmacSha1(string text, string consumerSecret, string tokenSecret)
    {
        var key = $"{PercentEncode(consumerSecret)}&{PercentEncode(tokenSecret)}";
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(text)));
    }

    private static bool FixedEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ab.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    public static string RandomToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public static string RandomVerifier() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

    public static IResult Error(string error, int status = StatusCodes.Status401Unauthorized) =>
        Results.Text(
            FormEncode(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["oauth_problem"] = error
                }), "application/x-www-form-urlencoded", statusCode: status);
}

public sealed class OAuthStore
{
    public object Gate { get; } = new();
    public Store Data { get; private set; }
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public OAuthStore(string path, IReadOnlyCollection<Client>? seedClients = null)
    {
        _path = path;
        Data = File.Exists(path)
            ? JsonSerializer.Deserialize<Store>(File.ReadAllText(path), JsonOptions) ?? Store.Seed(seedClients)
            : Store.Seed(seedClients);
        lock (Gate)
        {
            SaveUnsafe();
        }
    }

    public void Upsert(Action<Store> change)
    {
        lock (Gate)
        {
            change(Data);
            SaveUnsafe();
        }
    }

    public void SaveUnsafe()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}

public sealed class Store
{
    public List<Client> Clients { get; set; } = [];

    public Dictionary<string, RequestToken> RequestTokens { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, AccessToken> AccessTokens { get; set; } = new(StringComparer.Ordinal);
    public List<NonceSeen> Nonces { get; set; } = [];

    public static Store Seed(IReadOnlyCollection<Client>? seedClients) => new()
    {
        Clients = seedClients?.ToList() ?? []
    };
}

public sealed class Client
{
    public string Key { get; set; } = "";
    public string Secret { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Approved { get; set; } = true;
}

public sealed class RequestToken
{
    public string Token { get; set; } = "";
    public string Secret { get; set; } = "";
    public string ConsumerKey { get; set; } = "";
    public string Callback { get; set; } = "";
    public bool Authorized { get; set; }
    public string Verifier { get; set; } = "";
}

public sealed class AccessToken
{
    public string Token { get; set; } = "";
    public string Secret { get; set; } = "";
    public string ConsumerKey { get; set; } = "";
    public string User { get; set; } = "";
}

public sealed class NonceSeen
{
    public string Key { get; set; } = "";
    public long Timestamp { get; set; }
}

public sealed record ValidationResult(
    bool Ok,
    string? Error,
    Dictionary<string, List<string>> Parameters,
    Client? Consumer,
    RequestToken? RequestToken,
    AccessToken? AccessToken,
    string? BaseString)
{
    public static ValidationResult Fail(string error, Dictionary<string, List<string>> p,
        Client? c = null, RequestToken? rt = null, AccessToken? at = null, string? bs = null) =>
        new(false, error, p, c, rt, at, bs);

    public static ValidationResult Success(Dictionary<string, List<string>> p, Client c,
        RequestToken? rt, AccessToken? at, string bs) => new(true, null, p, c, rt, at, bs);
}
