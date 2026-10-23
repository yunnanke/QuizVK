using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Authentication;
using QuizMvp.Data;
using QuizMvp.Models;

namespace QuizMvp.Services;

/// <summary>
/// Рабочая реализация модуля регистрации и входа с хранением пользователей в EF Core.
/// </summary>
public sealed class AuthenticationModule : IAuthenticationModule
{
    private readonly ApplicationDbContext _context;
    private readonly AuthenticationModuleSettings _settings;

    public AuthenticationModule(ApplicationDbContext context, AuthenticationModuleSettings settings)
    {
        _context = context;
        _settings = settings;
    }

    public bool RegistrationEnabled => _settings.RegistrationEnabled;

    public async Task<RegistrationResult> RegisterAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!RegistrationEnabled)
        {
            return new RegistrationResult(RegistrationStatus.Disabled, null);
        }

        var username = request.Username ?? string.Empty;
        if (await _context.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return new RegistrationResult(RegistrationStatus.UsernameAlreadyExists, null);
        }

        var user = new User
        {
            Username = username,
            Email = request.Email ?? string.Empty,
            PasswordHash = HashPassword(request.Password ?? string.Empty),
            Role = request.Role ?? "Participant"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return new RegistrationResult(RegistrationStatus.Registered, ToAccount(user));
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username;
        var password = request.Password;
        var user = username is null
            ? null
            : await _context.Users.FirstOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);

        if (user is null || string.IsNullOrEmpty(password) || !VerifyPassword(password, user.PasswordHash))
        {
            return new AuthenticationResult(AuthenticationStatus.InvalidCredentials, null);
        }

        return new AuthenticationResult(AuthenticationStatus.Authenticated, ToAccount(user));
    }

    private static AuthenticatedAccount ToAccount(User user) =>
        new(user.Id, user.Username, user.Role);

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string hash) =>
        HashPassword(password) == hash;
}
