namespace QuizMvp.Models;

public sealed record AuthenticationModuleSettings(bool RegistrationEnabled)
{
    public static AuthenticationModuleSettings Parse(string? registrationEnabled)
    {
        if (!bool.TryParse(registrationEnabled, out var enabled))
        {
            throw new InvalidOperationException(
                "Параметр конфигурации 'Authentication:RegistrationEnabled' должен иметь значение true или false.");
        }

        return new AuthenticationModuleSettings(enabled);
    }
}
