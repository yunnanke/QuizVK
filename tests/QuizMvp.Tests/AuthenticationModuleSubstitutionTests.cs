using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizMvp.Authentication;
using QuizMvp.Data;
using QuizMvp.Models;
using QuizMvp.Tests.Infrastructure;
using Xunit;

namespace QuizMvp.Tests;

public sealed class AuthenticationModuleSubstitutionTests
{
    private const string SampleUsername = "modular-test-user";
    private const string SamplePassword = "Passw0rd!";

    [Theory]
    [InlineData(false, SampleUsername, SamplePassword, true)]
    [InlineData(true, SampleUsername, SamplePassword, true)]
    [InlineData(false, SampleUsername, "WrongPassword!", false)]
    [InlineData(true, SampleUsername, "WrongPassword!", false)]
    [InlineData(false, null, SamplePassword, false)]
    [InlineData(true, null, SamplePassword, false)]
    public async Task LoginScenario_WorksWithProductionAndSubstituteWithoutChangingConsumer(
        bool useSubstitute,
        string? username,
        string password,
        bool succeeds)
    {
        var substitute = useSubstitute ? new SuccessfulLoginSubstitute() : null;
        using var factory = new TestWebApplicationFactory(authenticationModuleOverride: substitute);

        if (!useSubstitute)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User
            {
                Username = SampleUsername,
                Email = "modular-test@example.test",
                PasswordHash = HashPassword(SamplePassword),
                Role = "Participant"
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var form = new Dictionary<string, string> { ["password"] = password };
        if (username is not null)
        {
            form["username"] = username;
        }
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));

        Assert.Equal(succeeds ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.StatusCode);
        if (succeeds)
        {
            Assert.Equal("/", response.Headers.Location?.ToString());
            Assert.Contains("Set-Cookie", response.Headers.Select(header => header.Key));
        }
        else
        {
            Assert.DoesNotContain("Set-Cookie", response.Headers.Select(header => header.Key));
        }
    }

    private static string HashPassword(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class SuccessfulLoginSubstitute : QuizMvp.Authentication.IAuthenticationModule
    {
        public bool RegistrationEnabled => true;

        public Task<RegistrationResult> RegisterAsync(
            RegistrationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegistrationResult(RegistrationStatus.Disabled, null));

        public Task<AuthenticationResult> AuthenticateAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            var isSampleLogin = request.Username == SampleUsername && request.Password == SamplePassword;
            var result = isSampleLogin
                ? new AuthenticationResult(
                    AuthenticationStatus.Authenticated,
                    new AuthenticatedAccount(42, SampleUsername, "Participant"))
                : new AuthenticationResult(AuthenticationStatus.InvalidCredentials, null);
            return Task.FromResult(result);
        }
    }
}
