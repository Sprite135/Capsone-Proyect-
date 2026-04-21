namespace LicitIA.Api.Contracts;

public sealed class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
