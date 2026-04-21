namespace LicitIA.Api.Configuration;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "LicitIA";

    public string Audience { get; set; } = "LicitIAUsers";

    public int ExpirationMinutes { get; set; } = 1440;
}
