namespace OperationsCenter.Domain.Identity;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, string email, string passwordHash, SystemRole role, bool isActive, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public SystemRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(string email, string passwordHash, SystemRole role, DateTimeOffset createdAt)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCreatedAt = createdAt.ToUniversalTime();

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new User(
            id: Guid.NewGuid(),
            email: normalizedEmail,
            passwordHash: passwordHash,
            role: role,
            isActive: true,
            createdAt: normalizedCreatedAt);
    }

    public bool MatchesEmail(string email)
    {
        return Email.Equals(NormalizeEmail(email), StringComparison.Ordinal);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > 320)
        {
            throw new ArgumentException("Email must not exceed 320 characters.", nameof(email));
        }

        return normalized;
    }
}
