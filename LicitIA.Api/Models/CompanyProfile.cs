using System.Text.Json.Serialization;

namespace LicitIA.Api.Models;

public sealed record CompanyProfile
{
    public int ProfileId { get; init; }

    public Guid? UserId { get; init; }

    public string CompanyName { get; init; } = string.Empty;

    public List<string> PreferredCategories { get; init; } = new();

    public List<string> PreferredLocations { get; init; } = new();

    public List<string> PreferredModalities { get; init; } = new();

    public decimal MinAmount { get; init; }

    public decimal MaxAmount { get; init; }

    public decimal IdealAmount { get; init; }

    public List<string> FavoriteEntities { get; init; } = new();

    public List<string> ExcludedEntities { get; init; } = new();

    public List<string> PreferredKeywords { get; init; } = new();

    public List<string> ExcludedKeywords { get; init; } = new();

    public string SeaceObjectDescription { get; init; } = string.Empty;

    public int SeaceCallYear { get; init; } = DateTime.UtcNow.Year;

    public string SeaceContractObject { get; init; } = string.Empty;

    public int MinDaysToClose { get; init; } = 3;

    public int MaxDaysToClose { get; init; } = 30;

    public int IdealDaysToClose { get; init; } = 15;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
