using LicitIA.Api.Models;
using System;
using System.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LicitIA.Api.Data;

public class CompanyProfileRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CompanyProfileRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Models.CompanyProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT ProfileId, UserId, CompanyName, PreferredCategories, PreferredLocations, PreferredModalities, 
                   MinAmount, MaxAmount, IdealAmount, FavoriteEntities, ExcludedEntities, 
                   PreferredKeywords, ExcludedKeywords, MinDaysToClose, MaxDaysToClose, IdealDaysToClose,
                   CreatedAtUtc, UpdatedAtUtc
            FROM dbo.CompanyProfile
            WHERE UserId = @UserId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapCompanyProfile(reader);
        }

        return null;
    }

    public async Task<Models.CompanyProfile?> GetDefaultProfileAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT TOP 1 ProfileId, UserId, CompanyName, PreferredCategories, PreferredLocations, PreferredModalities, 
                   MinAmount, MaxAmount, IdealAmount, FavoriteEntities, ExcludedEntities, 
                   PreferredKeywords, ExcludedKeywords, MinDaysToClose, MaxDaysToClose, IdealDaysToClose,
                   CreatedAtUtc, UpdatedAtUtc
            FROM dbo.CompanyProfile
            WHERE UserId IS NULL
            ORDER BY ProfileId;";

        await using var command = new SqlCommand(sql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapCompanyProfile(reader);
        }

        return null;
    }

    public async Task<int> InsertProfileAsync(Models.CompanyProfile profile, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO dbo.CompanyProfile (UserId, CompanyName, PreferredCategories, PreferredLocations, PreferredModalities, 
                                            MinAmount, MaxAmount, IdealAmount, FavoriteEntities, ExcludedEntities, 
                                            PreferredKeywords, ExcludedKeywords, MinDaysToClose, MaxDaysToClose, IdealDaysToClose, 
                                            CreatedAtUtc, UpdatedAtUtc)
            OUTPUT INSERTED.ProfileId
            VALUES (@UserId, @CompanyName, @PreferredCategories, @PreferredLocations, @PreferredModalities,
                    @MinAmount, @MaxAmount, @IdealAmount, @FavoriteEntities, @ExcludedEntities, 
                    @PreferredKeywords, @ExcludedKeywords, @MinDaysToClose, @MaxDaysToClose, @IdealDaysToClose,
                    GETUTCDATE(), GETUTCDATE());";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", (object?)profile.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CompanyName", profile.CompanyName);
        command.Parameters.AddWithValue("@PreferredCategories", JsonSerializer.Serialize(profile.PreferredCategories));
        command.Parameters.AddWithValue("@PreferredLocations", JsonSerializer.Serialize(profile.PreferredLocations));
        command.Parameters.AddWithValue("@PreferredModalities", JsonSerializer.Serialize(profile.PreferredModalities));
        command.Parameters.AddWithValue("@MinAmount", profile.MinAmount);
        command.Parameters.AddWithValue("@MaxAmount", profile.MaxAmount);
        command.Parameters.AddWithValue("@IdealAmount", profile.IdealAmount);
        command.Parameters.AddWithValue("@FavoriteEntities", JsonSerializer.Serialize(profile.FavoriteEntities));
        command.Parameters.AddWithValue("@ExcludedEntities", JsonSerializer.Serialize(profile.ExcludedEntities));
        command.Parameters.AddWithValue("@PreferredKeywords", JsonSerializer.Serialize(profile.PreferredKeywords));
        command.Parameters.AddWithValue("@ExcludedKeywords", JsonSerializer.Serialize(profile.ExcludedKeywords));
        command.Parameters.AddWithValue("@MinDaysToClose", profile.MinDaysToClose);
        command.Parameters.AddWithValue("@MaxDaysToClose", profile.MaxDaysToClose);
        command.Parameters.AddWithValue("@IdealDaysToClose", profile.IdealDaysToClose);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> UpdateProfileAsync(Models.CompanyProfile profile, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.CompanyProfile
            SET CompanyName = @CompanyName,
                PreferredCategories = @PreferredCategories,
                PreferredLocations = @PreferredLocations,
                PreferredModalities = @PreferredModalities,
                MinAmount = @MinAmount,
                MaxAmount = @MaxAmount,
                IdealAmount = @IdealAmount,
                FavoriteEntities = @FavoriteEntities,
                ExcludedEntities = @ExcludedEntities,
                PreferredKeywords = @PreferredKeywords,
                ExcludedKeywords = @ExcludedKeywords,
                MinDaysToClose = @MinDaysToClose,
                MaxDaysToClose = @MaxDaysToClose,
                IdealDaysToClose = @IdealDaysToClose,
                UpdatedAtUtc = GETUTCDATE()
            WHERE ProfileId = @ProfileId
              AND UserId = @UserId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProfileId", profile.ProfileId);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@CompanyName", profile.CompanyName);
        command.Parameters.AddWithValue("@PreferredCategories", JsonSerializer.Serialize(profile.PreferredCategories));
        command.Parameters.AddWithValue("@PreferredLocations", JsonSerializer.Serialize(profile.PreferredLocations));
        command.Parameters.AddWithValue("@PreferredModalities", JsonSerializer.Serialize(profile.PreferredModalities));
        command.Parameters.AddWithValue("@MinAmount", profile.MinAmount);
        command.Parameters.AddWithValue("@MaxAmount", profile.MaxAmount);
        command.Parameters.AddWithValue("@IdealAmount", profile.IdealAmount);
        command.Parameters.AddWithValue("@FavoriteEntities", JsonSerializer.Serialize(profile.FavoriteEntities));
        command.Parameters.AddWithValue("@ExcludedEntities", JsonSerializer.Serialize(profile.ExcludedEntities));
        command.Parameters.AddWithValue("@PreferredKeywords", JsonSerializer.Serialize(profile.PreferredKeywords));
        command.Parameters.AddWithValue("@ExcludedKeywords", JsonSerializer.Serialize(profile.ExcludedKeywords));
        command.Parameters.AddWithValue("@MinDaysToClose", profile.MinDaysToClose);
        command.Parameters.AddWithValue("@MaxDaysToClose", profile.MaxDaysToClose);
        command.Parameters.AddWithValue("@IdealDaysToClose", profile.IdealDaysToClose);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Models.CompanyProfile MapCompanyProfile(SqlDataReader reader)
    {
        T DeserializeList<T>(string columnName) where T : new()
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return new T();
            
            var json = reader.GetString(ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return new T();
            
            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        return new Models.CompanyProfile
        {
            ProfileId = reader.GetInt32(reader.GetOrdinal("ProfileId")),
            UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : reader.GetGuid(reader.GetOrdinal("UserId")),
            CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
            PreferredCategories = DeserializeList<List<string>>("PreferredCategories"),
            PreferredLocations = DeserializeList<List<string>>("PreferredLocations"),
            PreferredModalities = DeserializeList<List<string>>("PreferredModalities"),
            MinAmount = reader.GetDecimal(reader.GetOrdinal("MinAmount")),
            MaxAmount = reader.GetDecimal(reader.GetOrdinal("MaxAmount")),
            IdealAmount = reader.GetDecimal(reader.GetOrdinal("IdealAmount")),
            FavoriteEntities = DeserializeList<List<string>>("FavoriteEntities"),
            ExcludedEntities = DeserializeList<List<string>>("ExcludedEntities"),
            PreferredKeywords = DeserializeList<List<string>>("PreferredKeywords"),
            ExcludedKeywords = DeserializeList<List<string>>("ExcludedKeywords"),
            MinDaysToClose = reader.GetInt32(reader.GetOrdinal("MinDaysToClose")),
            MaxDaysToClose = reader.GetInt32(reader.GetOrdinal("MaxDaysToClose")),
            IdealDaysToClose = reader.GetInt32(reader.GetOrdinal("IdealDaysToClose")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc"))
        };
    }
}
