using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StrictDocOslcRm.Tests;

public class OAuth1CsrfTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _tempStorePath = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempStorePath = Path.Combine(Path.GetTempPath(), $"oauth-store-{Guid.NewGuid()}.json");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OAuth1:StorePath", _tempStorePath);
            });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (_tempStorePath != null && File.Exists(_tempStorePath))
        {
            try { File.Delete(_tempStorePath); } catch { }
        }
    }

    [Test]
    public async Task AuthorizePage_UnknownToken_ReturnsBadRequest()
    {
        // Act: Request authorization page for default seed client and unknown token
        var response = await _client.GetAsync("/oauth/authorize?oauth_token=test-token");

        var content = await response.Content.ReadAsStringAsync();

        // Unknown token returns Bad Request ("unknown oauth_token")
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(content).Contains("unknown oauth_token");
    }

    [Test]
    public async Task ApproveConsumerKeyPage_RendersAntiforgeryToken()
    {
        // Act: Request approve consumer key page
        var response = await _client.GetAsync("/oauth/approve_consumer_key?oauth_consumer_key=jazz");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        // Check for anti-forgery hidden input field
        await Assert.That(content).Contains("name=\"__RequestVerificationToken\"");

        // Check for anti-forgery cookie in response headers
        await Assert.That(response.Headers.Contains("Set-Cookie")).IsTrue();
    }

    [Test]
    public async Task AuthorizeDecision_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        // Act: Post decision without anti-forgery token
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("oauth_token", "some-token"),
            new KeyValuePair<string, string>("decision", "approve")
        });

        var response = await _client.PostAsync("/oauth/authorize", formContent);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("CSRF validation failed");
    }

    [Test]
    public async Task ApproveConsumerKeyDecision_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        // Act: Post decision without anti-forgery token
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("oauth_consumer_key", "jazz"),
            new KeyValuePair<string, string>("decision", "approve")
        });

        var response = await _client.PostAsync("/oauth/approve_consumer_key", formContent);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("CSRF validation failed");
    }

    [Test]
    public async Task ApproveConsumerKeyDecision_WithValidAntiforgeryToken_Succeeds()
    {
        // Arrange: GET approval page to get cookie and form token
        var getResponse = await _client.GetAsync("/oauth/approve_consumer_key?oauth_consumer_key=jazz");
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var getHtml = await getResponse.Content.ReadAsStringAsync();

        // Extract anti-forgery token value
        var match = Regex.Match(getHtml, @"name=""__RequestVerificationToken"" value=""([^""]+)""");
        await Assert.That(match.Success).IsTrue();
        var tokenValue = match.Groups[1].Value;

        // Extract cookie header
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        await Assert.That(cookieHeader).IsNotNull();

        // Act: POST decision with valid token and cookie
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/approve_consumer_key");
        request.Headers.Add("Cookie", cookieHeader);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", tokenValue),
            new KeyValuePair<string, string>("oauth_consumer_key", "jazz"),
            new KeyValuePair<string, string>("decision", "approve")
        });

        var postResponse = await _client.SendAsync(request);

        await Assert.That(postResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var postContent = await postResponse.Content.ReadAsStringAsync();
        await Assert.That(postContent).Contains("Consumer key approved");
    }
}
