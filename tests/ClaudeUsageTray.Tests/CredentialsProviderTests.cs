using ClaudeUsageTray.Infrastructure;
using Xunit;

namespace ClaudeUsageTray.Tests;

public class CredentialsProviderTests
{
    [Fact]
    public void ParsesNestedOAuthContainer()
    {
        const string json = """
            {
              "claudeAiOauth": {
                "accessToken": "test-token-not-a-secret",
                "expiresAt": 1893456000000,
                "subscriptionType": "max"
              }
            }
            """;

        var credentials = CredentialsProvider.TryParse(json, "test");

        Assert.NotNull(credentials);
        Assert.Equal("test-token-not-a-secret", credentials!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1893456000000), credentials.ExpiresAt);
        Assert.Equal("max", credentials.SubscriptionType);
    }

    [Fact]
    public void ParsesFlatSnakeCaseToken()
    {
        const string json = """{ "access_token": "flat", "expires_at": 1893456000 }""";

        var credentials = CredentialsProvider.TryParse(json, "test");

        Assert.NotNull(credentials);
        Assert.Equal("flat", credentials!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), credentials.ExpiresAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "claudeAiOauth": { "subscriptionType": "max" } }""")]
    public void ReturnsNullWhenNoAccessToken(string json)
    {
        Assert.Null(CredentialsProvider.TryParse(json, "test"));
    }
}
