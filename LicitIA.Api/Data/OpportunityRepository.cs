using LicitIA.Api.Models;
using System.Data.SqlClient;

namespace LicitIA.Api.Data;

public sealed class OpportunityRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public OpportunityRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Opportunity>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                OpportunityId,
                ProcessCode,
                Title,
                EntityName,
                EstimatedAmount,
                ClosingDate,
                Category,
                Modality,
                MatchScore,
                MatchedKeywordsCount,
                Summary,
                Location,
                IsPriority,
                PublishedDate,
                SeaceIndex,
                SelectionType,
                ConvocationNumber,
                ApplicableRegulation,
                SeaceVersion,
                EntityLegalAddress,
                EntityWebsite,
                EntityPhone,
                ContractObject,
                ParticipationCost,
                BasesReproductionCost,
                SeaceDetailJson,
                SeaceScheduleJson
            FROM dbo.Opportunities
            ORDER BY SeaceIndex ASC;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<Opportunity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapOpportunity(reader));
        }

        return items;
    }

    public async Task<Opportunity?> GetByIdAsync(int opportunityId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                OpportunityId,
                ProcessCode,
                Title,
                EntityName,
                EstimatedAmount,
                ClosingDate,
                Category,
                Modality,
                MatchScore,
                MatchedKeywordsCount,
                Summary,
                Location,
                IsPriority,
                PublishedDate,
                SeaceIndex,
                SelectionType,
                ConvocationNumber,
                ApplicableRegulation,
                SeaceVersion,
                EntityLegalAddress,
                EntityWebsite,
                EntityPhone,
                ContractObject,
                ParticipationCost,
                BasesReproductionCost,
                SeaceDetailJson,
                SeaceScheduleJson
            FROM dbo.Opportunities
            WHERE OpportunityId = @OpportunityId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@OpportunityId", opportunityId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapOpportunity(reader);
    }

    public async Task<Opportunity?> GetByProcessCodeAsync(string processCode, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (1)
                OpportunityId,
                ProcessCode,
                Title,
                EntityName,
                EstimatedAmount,
                ClosingDate,
                Category,
                Modality,
                MatchScore,
                MatchedKeywordsCount,
                Summary,
                Location,
                IsPriority,
                PublishedDate,
                SeaceIndex,
                SelectionType,
                ConvocationNumber,
                ApplicableRegulation,
                SeaceVersion,
                EntityLegalAddress,
                EntityWebsite,
                EntityPhone,
                ContractObject,
                ParticipationCost,
                BasesReproductionCost,
                SeaceDetailJson,
                SeaceScheduleJson
            FROM dbo.Opportunities
            WHERE ProcessCode = @ProcessCode;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessCode", processCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapOpportunity(reader);
    }

    public async Task<DateTime?> GetLatestPublishedDateAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT MAX(PublishedDate) AS LatestPublishedDate
            FROM dbo.Opportunities
            WHERE PublishedDate IS NOT NULL;
            """;

        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result == null || result == DBNull.Value)
        {
            return null;
        }

        return (DateTime)result;
    }

    public async Task InsertOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Opportunities
            (
                ProcessCode,
                Title,
                EntityName,
                EstimatedAmount,
                ClosingDate,
                Category,
                Modality,
                MatchScore,
                MatchedKeywordsCount,
                Summary,
                Location,
                IsPriority,
                PublishedDate,
                SeaceIndex,
                SelectionType,
                ConvocationNumber,
                ApplicableRegulation,
                SeaceVersion,
                EntityLegalAddress,
                EntityWebsite,
                EntityPhone,
                ContractObject,
                ParticipationCost,
                BasesReproductionCost,
                SeaceDetailJson,
                SeaceScheduleJson
            )
            VALUES
            (
                @ProcessCode,
                @Title,
                @EntityName,
                @EstimatedAmount,
                @ClosingDate,
                @Category,
                @Modality,
                @MatchScore,
                @MatchedKeywordsCount,
                @Summary,
                @Location,
                @IsPriority,
                @PublishedDate,
                @SeaceIndex,
                @SelectionType,
                @ConvocationNumber,
                @ApplicableRegulation,
                @SeaceVersion,
                @EntityLegalAddress,
                @EntityWebsite,
                @EntityPhone,
                @ContractObject,
                @ParticipationCost,
                @BasesReproductionCost,
                @SeaceDetailJson,
                @SeaceScheduleJson
            );
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessCode", opportunity.ProcessCode);
        command.Parameters.AddWithValue("@Title", opportunity.Title);
        command.Parameters.AddWithValue("@EntityName", opportunity.EntityName);
        command.Parameters.AddWithValue("@EstimatedAmount", opportunity.EstimatedAmount);
        command.Parameters.AddWithValue("@ClosingDate", opportunity.ClosingDate);
        command.Parameters.AddWithValue("@Category", opportunity.Category);
        command.Parameters.AddWithValue("@Modality", opportunity.Modality);
        command.Parameters.AddWithValue("@MatchScore", opportunity.MatchScore);
        command.Parameters.AddWithValue("@MatchedKeywordsCount", opportunity.MatchedKeywordsCount);
        command.Parameters.AddWithValue("@Summary", opportunity.Summary);
        command.Parameters.AddWithValue("@Location", opportunity.Location);
        command.Parameters.AddWithValue("@IsPriority", opportunity.IsPriority);
        command.Parameters.AddWithValue("@PublishedDate", opportunity.PublishedDate);
        command.Parameters.AddWithValue("@SeaceIndex", opportunity.SeaceIndex);
        command.Parameters.AddWithValue("@SelectionType", opportunity.SelectionType);
        command.Parameters.AddWithValue("@ConvocationNumber", opportunity.ConvocationNumber);
        command.Parameters.AddWithValue("@ApplicableRegulation", opportunity.ApplicableRegulation);
        command.Parameters.AddWithValue("@SeaceVersion", opportunity.SeaceVersion);
        command.Parameters.AddWithValue("@EntityLegalAddress", opportunity.EntityLegalAddress);
        command.Parameters.AddWithValue("@EntityWebsite", opportunity.EntityWebsite);
        command.Parameters.AddWithValue("@EntityPhone", opportunity.EntityPhone);
        command.Parameters.AddWithValue("@ContractObject", opportunity.ContractObject);
        command.Parameters.AddWithValue("@ParticipationCost", opportunity.ParticipationCost);
        command.Parameters.AddWithValue("@BasesReproductionCost", opportunity.BasesReproductionCost);
        command.Parameters.AddWithValue("@SeaceDetailJson", opportunity.SeaceDetailJson);
        command.Parameters.AddWithValue("@SeaceScheduleJson", opportunity.SeaceScheduleJson);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAllOpportunitiesAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM dbo.Opportunities;";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateMatchScoreAsync(int opportunityId, int matchScore, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE dbo.Opportunities SET MatchScore = @MatchScore WHERE OpportunityId = @OpportunityId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MatchScore", matchScore);
        command.Parameters.AddWithValue("@OpportunityId", opportunityId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateMatchScoreAndCountAsync(int opportunityId, int matchScore, int matchedKeywordsCount, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE dbo.Opportunities SET MatchScore = @MatchScore, MatchedKeywordsCount = @MatchedKeywordsCount WHERE OpportunityId = @OpportunityId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MatchScore", matchScore);
        command.Parameters.AddWithValue("@MatchedKeywordsCount", matchedKeywordsCount);
        command.Parameters.AddWithValue("@OpportunityId", opportunityId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Opportunity MapOpportunity(SqlDataReader reader) =>
        new()
        {
            OpportunityId = reader.GetInt32(reader.GetOrdinal("OpportunityId")),
            ProcessCode = reader.GetString(reader.GetOrdinal("ProcessCode")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
            EstimatedAmount = reader.GetDecimal(reader.GetOrdinal("EstimatedAmount")),
            ClosingDate = reader.GetDateTime(reader.GetOrdinal("ClosingDate")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            Modality = reader.GetString(reader.GetOrdinal("Modality")),
            MatchScore = reader.GetInt32(reader.GetOrdinal("MatchScore")),
            MatchedKeywordsCount = reader.GetInt32(reader.GetOrdinal("MatchedKeywordsCount")),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            Location = reader.GetString(reader.GetOrdinal("Location")),
            IsPriority = reader.GetBoolean(reader.GetOrdinal("IsPriority")),
            PublishedDate = reader.IsDBNull(reader.GetOrdinal("PublishedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("PublishedDate")),
            SeaceIndex = reader.IsDBNull(reader.GetOrdinal("SeaceIndex")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("SeaceIndex")),
            SelectionType = GetString(reader, "SelectionType"),
            ConvocationNumber = GetString(reader, "ConvocationNumber"),
            ApplicableRegulation = GetString(reader, "ApplicableRegulation"),
            SeaceVersion = GetString(reader, "SeaceVersion"),
            EntityLegalAddress = GetString(reader, "EntityLegalAddress"),
            EntityWebsite = GetString(reader, "EntityWebsite"),
            EntityPhone = GetString(reader, "EntityPhone"),
            ContractObject = GetString(reader, "ContractObject"),
            ParticipationCost = GetString(reader, "ParticipationCost"),
            BasesReproductionCost = GetString(reader, "BasesReproductionCost"),
            SeaceDetailJson = GetString(reader, "SeaceDetailJson"),
            SeaceScheduleJson = GetString(reader, "SeaceScheduleJson")
        };

    private static string GetString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }
}
