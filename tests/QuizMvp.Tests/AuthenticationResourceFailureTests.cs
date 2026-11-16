using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Authentication;
using QuizMvp.Data;
using QuizMvp.Models;
using QuizMvp.Services;
using QuizMvp.Tests.Infrastructure;
using System.Net;
using Xunit;

namespace QuizMvp.Tests;

public sealed class AuthenticationResourceFailureTests
{
    [Fact]
    public async Task RegisterAsync_SuccessfullyUsesAndReleasesSqliteDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"quizmvp-auth-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions($"Data Source={databasePath};Pooling=False");
            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.EnsureCreatedAsync();
                var module = new AuthenticationModule(context, new AuthenticationModuleSettings(true));

                var result = await module.RegisterAsync(
                    new RegistrationRequest("resource-user", "resource@example.test", "Secure!Pass1", "Participant"));

                Assert.Equal(RegistrationStatus.Registered, result.Status);
                Assert.NotNull(result.Account);
                Assert.Equal(1, await context.Users.CountAsync());
            }

            await using var verification = new ApplicationDbContext(CreateOptions($"Data Source={databasePath};Mode=ReadOnly;Pooling=False"));
            Assert.Equal(1, await verification.Users.CountAsync());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task RegisterAsync_WhenDatabaseCannotBeOpened_ReturnsUnavailableWithoutCreatingAccount()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), $"quizmvp-missing-{Guid.NewGuid():N}");
        var options = CreateOptions($"Data Source={Path.Combine(missingDirectory, "auth.db")}");
        await using var context = new ApplicationDbContext(options);
        var module = new AuthenticationModule(context, new AuthenticationModuleSettings(true));

        var result = await module.RegisterAsync(
            new RegistrationRequest("resource-user", "resource@example.test", "Secure!Pass1", "Participant"));

        Assert.Equal(RegistrationStatus.StorageUnavailable, result.Status);
        Assert.Null(result.Account);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(Directory.Exists(missingDirectory));
    }

    [Fact]
    public async Task RegisterAsync_WhenDatabaseRejectsWrite_ReturnsUnavailableAndRollsBackResult()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER reject_user_insert BEFORE INSERT ON Users BEGIN SELECT RAISE(ABORT, 'simulated write failure'); END;");
        var module = new AuthenticationModule(context, new AuthenticationModuleSettings(true));

        var result = await module.RegisterAsync(
            new RegistrationRequest("resource-user", "resource@example.test", "Secure!Pass1", "Participant"));

        Assert.Equal(RegistrationStatus.StorageUnavailable, result.Status);
        Assert.Null(result.Account);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task RegisterAsync_OnEarlyValidationReturnDoesNotOpenDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var options = CreateOptions(connection);
        await using var context = new ApplicationDbContext(options);
        var module = new AuthenticationModule(context, new AuthenticationModuleSettings(true));

        var result = await module.RegisterAsync(
            new RegistrationRequest("resource-user", "resource@example.test", "weakpass", "Participant"));

        Assert.Equal(RegistrationStatus.InvalidPassword, result.Status);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AccountEndpoints_WhenStorageIsUnavailable_Return503WithoutSessionCookie()
    {
        using var factory = new TestWebApplicationFactory(
            authenticationModuleOverride: new UnavailableAuthenticationModule());
        using var client = factory.CreateClient();

        var register = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "resource-user",
            ["email"] = "resource@example.test",
            ["password"] = "Secure!Pass1",
            ["role"] = "Participant"
        }));
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "resource-user",
            ["password"] = "Secure!Pass1"
        }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, register.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.DoesNotContain("Set-Cookie", register.Headers.Select(header => header.Key));
        Assert.DoesNotContain("Set-Cookie", login.Headers.Select(header => header.Key));
        Assert.Contains("Регистрация не выполнена", WebUtility.HtmlDecode(await register.Content.ReadAsStringAsync()));
        Assert.Contains("Хранилище временно недоступно", WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync()));
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;

    private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;

    private sealed class UnavailableAuthenticationModule : QuizMvp.Authentication.IAuthenticationModule
    {
        public bool RegistrationEnabled => true;

        public Task<RegistrationResult> RegisterAsync(
            RegistrationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegistrationResult(RegistrationStatus.StorageUnavailable, null));

        public Task<AuthenticationResult> AuthenticateAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthenticationResult(AuthenticationStatus.StorageUnavailable, null));
    }
}
