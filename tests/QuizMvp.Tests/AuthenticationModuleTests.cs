using System.Net;
using System.Net.Http.Json;
using QuizMvp.Models;
using QuizMvp.Tests.Infrastructure;
using Xunit;

namespace QuizMvp.Tests;

public sealed class AuthenticationModuleTests
{
    [Fact]
    public async Task Diagnostics_ReturnsVersionRevisionBuildAndRegistrationSetting()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_diagnostics/authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<AuthenticationDiagnostics>();
        Assert.NotNull(details);
        Assert.Equal("ready", details.Status);
        Assert.Equal("Authentication", details.Module);
        Assert.True(details.RegistrationEnabled);
        Assert.StartsWith("1.0.0+", details.Version);
        Assert.Equal(details.Version.Split('+', 2)[1], details.Revision);
        Assert.Contains(details.BuildConfiguration, new[] { "Debug", "Release" });
    }

    [Fact]
    public async Task RegisterEndpoints_DisabledByConfiguration_ReturnServiceUnavailable()
    {
        using var factory = new TestWebApplicationFactory(registrationEnabled: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var page = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Регистрация временно отключена", page);

        var postResponse = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = "disabled-user",
                ["email"] = "disabled@example.test",
                ["password"] = "not-a-real-password",
                ["role"] = "Participant"
            }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, postResponse.StatusCode);

        var details = await client.GetFromJsonAsync<AuthenticationDiagnostics>("/_diagnostics/authentication");
        Assert.NotNull(details);
        Assert.False(details.RegistrationEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yes")]
    public void Parse_RejectsInvalidRegistrationConfiguration(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthenticationModuleSettings.Parse(value));

        Assert.Contains("Authentication:RegistrationEnabled", exception.Message);
    }

    private sealed record AuthenticationDiagnostics(
        string Status,
        string Module,
        bool RegistrationEnabled,
        string Version,
        string Revision,
        string BuildConfiguration);
}
