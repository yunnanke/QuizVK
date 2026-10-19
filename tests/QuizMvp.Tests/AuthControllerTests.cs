using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizMvp.Data;
using QuizMvp.Models;
using QuizMvp.Tests.Infrastructure;
using Xunit;

namespace QuizMvp.Tests;

/// <summary>Integration tests for the current MVC session-auth implementation.</summary>
public sealed class AuthControllerTests
{
    private static HttpClient CreateClient(TestWebApplicationFactory factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<int> UserCountAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync();
    }

    private static async Task<User?> FindUserAsync(TestWebApplicationFactory factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.SingleOrDefaultAsync(user => user.Username == username);
    }

    private static string Sha256Base64(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static async Task RegisterAsync(HttpClient client, string username, string password = "Passw0rd!")
    {
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["email"] = $"{username}@example.test",
            ["password"] = password,
            ["role"] = "Participant"
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Register_ValidData_PersistsHashedUserAndRedirects()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);

        await RegisterAsync(client, "alice_test");

        (await UserCountAsync(factory)).Should().Be(1);
        var user = await FindUserAsync(factory, "alice_test");
        user.Should().NotBeNull();
        user!.Email.Should().Be("alice_test@example.test");
        user.PasswordHash.Should().Be(Sha256Base64("Passw0rd!"));
        user.PasswordHash.Should().NotBe("Passw0rd!");
        user.PasswordHash.Length.Should().Be(44);
        user.Role.Should().Be("Participant");
    }

    [Fact]
    public async Task Register_Get_ReturnsRegistrationForm()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Account/Register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateUsername_DoesNotCreateSecondUser()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await RegisterAsync(client, "same_user");

        var duplicate = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "same_user", ["email"] = "second@example.test",
            ["password"] = "Passw0rd!", ["role"] = "Participant"
        }));

        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserCountAsync(factory)).Should().Be(1);
        (await FindUserAsync(factory, "same_user"))!.Email.Should().Be("same_user@example.test");
    }

    [Fact]
    public async Task Register_DuplicateEmailWithDifferentUsernames_CurrentBehaviorAllowsDuplicate()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        foreach (var username in new[] { "email_owner_1", "email_owner_2" })
        {
            var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username, ["email"] = "shared@example.test",
                ["password"] = "Passw0rd!", ["role"] = "Participant"
            }));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }
        (await UserCountAsync(factory)).Should().Be(2);
    }

    [Fact]
    public async Task Login_ValidCredentials_RedirectsAndSetsSessionCookie()
    {
        using var factory = new TestWebApplicationFactory();
        using var registrationClient = CreateClient(factory);
        await RegisterAsync(registrationClient, "login_user");

        using var loginClient = CreateClient(factory);
        var response = await loginClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "login_user", ["password"] = "Passw0rd!"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/");
        response.Headers.Contains("Set-Cookie").Should().BeTrue();
    }

    [Fact]
    public async Task Login_Get_ReturnsLoginForm()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Incorrect!")]
    [InlineData("Passw0rd")]
    [InlineData("passw0rd!")]
    [InlineData("")]
    public async Task Login_WrongPassword_ShowsLoginViewWithoutCreatingOrChangingUser(string password)
    {
        using var factory = new TestWebApplicationFactory();
        using var registrationClient = CreateClient(factory);
        await RegisterAsync(registrationClient, "wrong_password_user");
        var countBefore = await UserCountAsync(factory);

        using var loginClient = CreateClient(factory);
        var response = await loginClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "wrong_password_user", ["password"] = password
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
        (await UserCountAsync(factory)).Should().Be(countBefore);
    }

    [Fact]
    public async Task Login_UnknownUsername_ShowsLoginViewAndDoesNotCreateUser()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "missing_user", ["password"] = "Passw0rd!"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserCountAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Register_RoleOmitted_UsesParticipantDefault()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "default_role_user", ["email"] = "role@example.test", ["password"] = "Passw0rd!"
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await FindUserAsync(factory, "default_role_user"))!.Role.Should().Be("Participant");
    }

    [Fact]
    public async Task Logout_ClearsSessionAndKeepsUserRecord()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await RegisterAsync(client, "logout_user");

        var response = await client.GetAsync("/Account/Logout");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("Login");
        (await UserCountAsync(factory)).Should().Be(1);
    }
}
