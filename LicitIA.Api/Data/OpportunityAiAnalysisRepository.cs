using System.Data.SqlClient;
using LicitIA.Api.Models;

namespace LicitIA.Api.Data;

public sealed class OpportunityAiAnalysisRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public OpportunityAiAnalysisRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OpportunityAiAnalysis?> GetByUserAndOpportunityAsync(Guid userId, int opportunityId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (1)
                AnalysisId, UserId, OpportunityId, ModelName, Recommendation,
                Summary, Risks, Requirements, NextSteps, RawResponse,
                CreatedAtUtc, UpdatedAtUtc
            FROM dbo.OpportunityAiAnalysis
            WHERE UserId = @UserId AND OpportunityId = @OpportunityId
            ORDER BY UpdatedAtUtc DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@OpportunityId", opportunityId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<int> CountCreatedTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.OpportunityAiAnalysis
            WHERE UserId = @UserId
              AND CreatedAtUtc >= @StartUtc
              AND CreatedAtUtc < @EndUtc;
            """;

        var now = DateTime.UtcNow;
        var startUtc = now.Date;
        var endUtc = startUtc.AddDays(1);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartUtc", startUtc);
        command.Parameters.AddWithValue("@EndUtc", endUtc);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task UpsertAsync(OpportunityAiAnalysis analysis, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            IF EXISTS (
                SELECT 1 FROM dbo.OpportunityAiAnalysis
                WHERE UserId = @UserId AND OpportunityId = @OpportunityId
            )
            BEGIN
                UPDATE dbo.OpportunityAiAnalysis
                SET ModelName = @ModelName,
                    Recommendation = @Recommendation,
                    Summary = @Summary,
                    Risks = @Risks,
                    Requirements = @Requirements,
                    NextSteps = @NextSteps,
                    RawResponse = @RawResponse,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE UserId = @UserId AND OpportunityId = @OpportunityId;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.OpportunityAiAnalysis
                    (UserId, OpportunityId, ModelName, Recommendation, Summary, Risks, Requirements, NextSteps, RawResponse, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    (@UserId, @OpportunityId, @ModelName, @Recommendation, @Summary, @Risks, @Requirements, @NextSteps, @RawResponse, SYSUTCDATETIME(), SYSUTCDATETIME());
            END
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", analysis.UserId);
        command.Parameters.AddWithValue("@OpportunityId", analysis.OpportunityId);
        command.Parameters.AddWithValue("@ModelName", analysis.ModelName);
        command.Parameters.AddWithValue("@Recommendation", analysis.Recommendation);
        command.Parameters.AddWithValue("@Summary", analysis.Summary);
        command.Parameters.AddWithValue("@Risks", analysis.Risks);
        command.Parameters.AddWithValue("@Requirements", analysis.Requirements);
        command.Parameters.AddWithValue("@NextSteps", analysis.NextSteps);
        command.Parameters.AddWithValue("@RawResponse", analysis.RawResponse);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OpportunityAiAnalysis Map(SqlDataReader reader) =>
        new()
        {
            AnalysisId = reader.GetInt32(reader.GetOrdinal("AnalysisId")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            OpportunityId = reader.GetInt32(reader.GetOrdinal("OpportunityId")),
            ModelName = reader.GetString(reader.GetOrdinal("ModelName")),
            Recommendation = reader.GetString(reader.GetOrdinal("Recommendation")),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            Risks = reader.GetString(reader.GetOrdinal("Risks")),
            Requirements = reader.GetString(reader.GetOrdinal("Requirements")),
            NextSteps = reader.GetString(reader.GetOrdinal("NextSteps")),
            RawResponse = reader.GetString(reader.GetOrdinal("RawResponse")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc"))
        };
}
