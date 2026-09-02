using System.Net.Mail;
using AirSms.Domain.Common;
using AirSms.Domain.Enums;

namespace AirSms.Domain.Entities;

public class User : BaseEntity
{
    private User()
    {
        Email = null!;
        PasswordHash = null!;
        FirstName = null!;
        LastName = null!;
    }

    public User(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role)
    {
        Email = NormalizeEmail(email);
        PasswordHash = Required(passwordHash, nameof(passwordHash));
        FirstName = RequiredName(firstName, nameof(firstName));
        LastName = RequiredName(lastName, nameof(lastName));

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
        IsActive = true;
    }

    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var trimmed = email.Trim();
        if (trimmed.Length > 254 ||
            !MailAddress.TryCreate(trimmed, out var parsed) ||
            !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email is invalid.", nameof(email));
        }

        return parsed.Address.ToLowerInvariant();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated();
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value;
    }

    private static string RequiredName(string value, string parameterName)
    {
        var name = Required(value, parameterName).Trim();
        if (name.Length > 100)
        {
            throw new ArgumentException($"{parameterName} cannot exceed 100 characters.", parameterName);
        }

        return name;
    }
}
