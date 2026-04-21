namespace LicitIA.Api.Models;

public sealed class AppUser
{
    public Guid UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public byte[] PasswordHash { get; init; } = Array.Empty<byte>();

    public byte[] PasswordSalt { get; init; } = Array.Empty<byte>();

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
