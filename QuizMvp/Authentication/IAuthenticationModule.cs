namespace QuizMvp.Authentication;

/// <summary>
/// Публичная граница модуля регистрации и входа в систему.
/// </summary>
/// <remarks>
/// Модуль не управляет HTTP-ответами и пользовательской сессией. Эти обязанности
/// остаются у <c>AccountController</c>.
/// </remarks>
public interface IAuthenticationModule
{
    /// <summary>Возвращает признак доступности регистрации в текущей конфигурации.</summary>
    bool RegistrationEnabled { get; }

    /// <summary>
    /// Регистрирует пользователя. Возвращает один из исходов: создана учётная запись,
    /// регистрация отключена или имя уже занято. При успехе возвращает данные для сессии.
    /// </summary>
    Task<RegistrationResult> RegisterAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет имя и пароль. При отсутствии пользователя или несовпадении пароля
    /// возвращает <see cref="AuthenticationStatus.InvalidCredentials"/>.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Данные, передаваемые при регистрации.</summary>
public sealed record RegistrationRequest(string? Username, string? Email, string? Password, string? Role);

/// <summary>Возможные исходы регистрации.</summary>
public enum RegistrationStatus
{
    Registered,
    Disabled,
    UsernameAlreadyExists
}

/// <summary>Исход регистрации и данные новой учётной записи при успехе.</summary>
public sealed record RegistrationResult(RegistrationStatus Status, AuthenticatedAccount? Account);

/// <summary>Данные для входа.</summary>
public sealed record LoginRequest(string? Username, string? Password);

/// <summary>Возможные исходы проверки учётных данных.</summary>
public enum AuthenticationStatus
{
    Authenticated,
    InvalidCredentials
}

/// <summary>Исход проверки учётных данных и сведения пользователя при успехе.</summary>
public sealed record AuthenticationResult(AuthenticationStatus Status, AuthenticatedAccount? Account);

/// <summary>Минимальные сведения пользователя, которые нужны веб-слою для создания сессии.</summary>
public sealed record AuthenticatedAccount(int Id, string Username, string Role);
