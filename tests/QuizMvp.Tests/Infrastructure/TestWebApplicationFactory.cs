using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizMvp.Authentication;
using QuizMvp.Data;
using QuizMvp.Models;

namespace QuizMvp.Tests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestWebApplicationFactory(
        bool? registrationEnabled = null,
        IAuthenticationModule? authenticationModuleOverride = null)
    {
        RegistrationEnabled = registrationEnabled;
        AuthenticationModuleOverride = authenticationModuleOverride;
        _connection.Open();
    }

    private bool? RegistrationEnabled { get; }
    private IAuthenticationModule? AuthenticationModuleOverride { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            if (AuthenticationModuleOverride is not null)
            {
                services.RemoveAll<IAuthenticationModule>();
                services.AddSingleton(AuthenticationModuleOverride);
            }

            if (RegistrationEnabled is bool registrationEnabled)
            {
                services.RemoveAll<AuthenticationModuleSettings>();
                services.AddSingleton(new AuthenticationModuleSettings(registrationEnabled));
            }

            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
