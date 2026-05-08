using LicitIA.Api.Models;
using System;
using System.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LicitIA.Api.Data;

public class AlertRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public AlertRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<AlertRule>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT RuleId, UserId, Name, Trigger, ConditionsJson, ChannelsJson, 
                   RecipientsJson, MessageTemplate, IsActive, CreatedAtUtc, 
                   LastTriggeredAtUtc, TriggerCount
            FROM dbo.AlertRule
            WHERE UserId = @UserId
            ORDER BY CreatedAtUtc DESC;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var rules = new List<AlertRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(MapAlertRule(reader));
        }

        return rules;
    }

    public async Task<AlertRule?> GetByIdAsync(int ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT RuleId, UserId, Name, Trigger, ConditionsJson, ChannelsJson, 
                   RecipientsJson, MessageTemplate, IsActive, CreatedAtUtc, 
                   LastTriggeredAtUtc, TriggerCount
            FROM dbo.AlertRule
            WHERE RuleId = @RuleId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RuleId", ruleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapAlertRule(reader);
        }

        return null;
    }

    public async Task<int> InsertAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO dbo.AlertRule (UserId, Name, Trigger, ConditionsJson, ChannelsJson, 
                                       RecipientsJson, MessageTemplate, IsActive, CreatedAtUtc, 
                                       LastTriggeredAtUtc, TriggerCount)
            OUTPUT INSERTED.RuleId
            VALUES (@UserId, @Name, @Trigger, @ConditionsJson, @ChannelsJson, 
                    @RecipientsJson, @MessageTemplate, @IsActive, @CreatedAtUtc, 
                    @LastTriggeredAtUtc, @TriggerCount);";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", rule.UserId);
        command.Parameters.AddWithValue("@Name", rule.Name);
        command.Parameters.AddWithValue("@Trigger", rule.Trigger);
        command.Parameters.AddWithValue("@ConditionsJson", rule.ConditionsJson);
        command.Parameters.AddWithValue("@ChannelsJson", rule.ChannelsJson);
        command.Parameters.AddWithValue("@RecipientsJson", rule.RecipientsJson);
        command.Parameters.AddWithValue("@MessageTemplate", rule.MessageTemplate);
        command.Parameters.AddWithValue("@IsActive", rule.IsActive);
        command.Parameters.AddWithValue("@CreatedAtUtc", rule.CreatedAtUtc);
        command.Parameters.AddWithValue("@LastTriggeredAtUtc", (object?)rule.LastTriggeredAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@TriggerCount", rule.TriggerCount);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task<int> UpdateAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.AlertRule
            SET Name = @Name, 
                Trigger = @Trigger, 
                ConditionsJson = @ConditionsJson, 
                ChannelsJson = @ChannelsJson, 
                RecipientsJson = @RecipientsJson, 
                MessageTemplate = @MessageTemplate, 
                IsActive = @IsActive,
                LastTriggeredAtUtc = @LastTriggeredAtUtc,
                TriggerCount = @TriggerCount
            WHERE RuleId = @RuleId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RuleId", rule.RuleId);
        command.Parameters.AddWithValue("@Name", rule.Name);
        command.Parameters.AddWithValue("@Trigger", rule.Trigger);
        command.Parameters.AddWithValue("@ConditionsJson", rule.ConditionsJson);
        command.Parameters.AddWithValue("@ChannelsJson", rule.ChannelsJson);
        command.Parameters.AddWithValue("@RecipientsJson", rule.RecipientsJson);
        command.Parameters.AddWithValue("@MessageTemplate", rule.MessageTemplate);
        command.Parameters.AddWithValue("@IsActive", rule.IsActive);
        command.Parameters.AddWithValue("@LastTriggeredAtUtc", (object?)rule.LastTriggeredAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@TriggerCount", rule.TriggerCount);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(int ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"DELETE FROM dbo.AlertRule WHERE RuleId = @RuleId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@RuleId", ruleId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetActiveCountByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT COUNT(*) 
            FROM dbo.AlertRule
            WHERE UserId = @UserId AND IsActive = 1;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task<int> GetTodayTriggeredCountByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT COUNT(*) 
            FROM dbo.AlertRule
            WHERE UserId = @UserId 
              AND LastTriggeredAtUtc >= CAST(GETUTCDATE() AS DATE);";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private static AlertRule MapAlertRule(SqlDataReader reader)
    {
        return new AlertRule
        {
            RuleId = reader.GetInt32(reader.GetOrdinal("RuleId")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Trigger = reader.GetString(reader.GetOrdinal("Trigger")),
            ConditionsJson = reader.GetString(reader.GetOrdinal("ConditionsJson")),
            ChannelsJson = reader.GetString(reader.GetOrdinal("ChannelsJson")),
            RecipientsJson = reader.GetString(reader.GetOrdinal("RecipientsJson")),
            MessageTemplate = reader.GetString(reader.GetOrdinal("MessageTemplate")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            LastTriggeredAtUtc = reader.IsDBNull(reader.GetOrdinal("LastTriggeredAtUtc")) 
                ? null 
                : reader.GetDateTime(reader.GetOrdinal("LastTriggeredAtUtc")),
            TriggerCount = reader.GetInt32(reader.GetOrdinal("TriggerCount"))
        };
    }
}
