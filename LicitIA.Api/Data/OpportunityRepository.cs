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
                Summary,
                Location,
                IsPriority
            FROM dbo.Opportunities
            ORDER BY MatchScore DESC, ClosingDate ASC, OpportunityId ASC;
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
                Summary,
                Location,
                IsPriority
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
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            Location = reader.GetString(reader.GetOrdinal("Location")),
            IsPriority = reader.GetBoolean(reader.GetOrdinal("IsPriority"))
        };
}
