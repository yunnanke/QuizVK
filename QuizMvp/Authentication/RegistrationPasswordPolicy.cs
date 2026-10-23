using System.Globalization;
using System.Text;

namespace QuizMvp.Authentication;

/// <summary>Правила пароля для создания новой учётной записи.</summary>
public static class RegistrationPasswordPolicy
{
    public const int MinimumLength = 8;
    public const string RequirementsMessage =
        "Не менее 8 символов, минимум одна буква любого алфавита и один специальный символ.";

    /// <summary>Проверяет длину, наличие буквы и наличие специального символа.</summary>
    public static bool IsValid(string? password)
    {
        if (password is null)
        {
            return false;
        }

        var length = 0;
        var hasLetter = false;
        var hasSpecialCharacter = false;

        foreach (var character in password.EnumerateRunes())
        {
            length++;
            hasLetter |= Rune.IsLetter(character);
            hasSpecialCharacter |= IsSpecialCharacter(character);
        }

        return length >= MinimumLength && hasLetter && hasSpecialCharacter;
    }

    private static bool IsSpecialCharacter(Rune character)
    {
        var category = Rune.GetUnicodeCategory(character);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation
            or UnicodeCategory.MathSymbol
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;
    }
}
