using Microsoft.AspNetCore.Identity;

namespace RosterHive.Data;

public class PolishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = "Wystąpił nieznany błąd." };

    public override IdentityError ConcurrencyFailure()
        => new() { Code = nameof(ConcurrencyFailure), Description = "Dane zostały zmienione przez innego użytkownika. Odśwież stronę i spróbuj ponownie." };

    public override IdentityError DuplicateUserName(string? userName)
        => new() { Code = nameof(DuplicateUserName), Description = "Taki użytkownik już istnieje." };

    public override IdentityError DuplicateEmail(string? email)
        => new() { Code = nameof(DuplicateEmail), Description = "Taki adres e-mail jest już zajęty." };

    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = "Nazwa użytkownika jest nieprawidłowa." };

    public override IdentityError InvalidEmail(string? email)
        => new() { Code = nameof(InvalidEmail), Description = "Adres e-mail jest nieprawidłowy." };

    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = "Użytkownik ma już ustawione hasło." };

    public override IdentityError UserLockoutNotEnabled()
        => new() { Code = nameof(UserLockoutNotEnabled), Description = "Blokada konta nie jest włączona dla tego użytkownika." };

    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = $"Hasło musi mieć co najmniej {length} znaków." };

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Hasło musi zawierać co najmniej jeden znak specjalny." };

    public override IdentityError PasswordRequiresDigit()
        => new() { Code = nameof(PasswordRequiresDigit), Description = "Hasło musi zawierać co najmniej jedną cyfrę (0-9)." };

    public override IdentityError PasswordRequiresLower()
        => new() { Code = nameof(PasswordRequiresLower), Description = "Hasło musi zawierać co najmniej jedną małą literę (a-z)." };

    public override IdentityError PasswordRequiresUpper()
        => new() { Code = nameof(PasswordRequiresUpper), Description = "Hasło musi zawierać co najmniej jedną wielką literę (A-Z)." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Hasło musi zawierać co najmniej {uniqueChars} unikalne znaki." };

    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = "Nieprawidłowe hasło." };

    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = "Token jest nieprawidłowy lub wygasł." };

    public override IdentityError LoginAlreadyAssociated()
        => new() { Code = nameof(LoginAlreadyAssociated), Description = "To konto logowania jest już przypisane do innego użytkownika." };

    public override IdentityError RecoveryCodeRedemptionFailed()
        => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Nie udało się użyć kodu odzyskiwania." };

    public override IdentityError UserAlreadyInRole(string? role)
        => new() { Code = nameof(UserAlreadyInRole), Description = "Użytkownik już ma przypisaną tę rolę." };

    public override IdentityError UserNotInRole(string? role)
        => new() { Code = nameof(UserNotInRole), Description = "Użytkownik nie ma przypisanej tej roli." };

    public override IdentityError DuplicateRoleName(string? role)
        => new() { Code = nameof(DuplicateRoleName), Description = "Taka rola już istnieje." };
}
