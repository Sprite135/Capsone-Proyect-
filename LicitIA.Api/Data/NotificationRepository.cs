using LicitIA.Api.Models;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace LicitIA.Api.Data;

public class NotificationRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public NotificationRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<PanelNotification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT NotificationId, UserId, Title, Message, Type, 
                   OpportunityProcessCode, OpportunityTitle, AffinityScore,
                   IsRead, CreatedAtUtc, ReadAtUtc
            FROM dbo.PanelNotification
            WHERE UserId = @UserId
            ORDER BY CreatedAtUtc DESC;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var notifications = new List<PanelNotification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notifications.Add(MapPanelNotification(reader));
        }

        return notifications;
    }

    public async Task<List<PanelNotification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT NotificationId, UserId, Title, Message, Type, 
                   OpportunityProcessCode, OpportunityTitle, AffinityScore,
                   IsRead, CreatedAtUtc, ReadAtUtc
            FROM dbo.PanelNotification
            WHERE UserId = @UserId AND IsRead = 0
            ORDER BY CreatedAtUtc DESC;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var notifications = new List<PanelNotification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notifications.Add(MapPanelNotification(reader));
        }

        return notifications;
    }

    public async Task<int> InsertAsync(PanelNotification notification, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO dbo.PanelNotification (UserId, Title, Message, Type, 
                                             OpportunityProcessCode, OpportunityTitle, AffinityScore,
                                             IsRead, CreatedAtUtc, ReadAtUtc)
            OUTPUT INSERTED.NotificationId
            VALUES (@UserId, @Title, @Message, @Type, 
                    @OpportunityProcessCode, @OpportunityTitle, @AffinityScore,
                    @IsRead, @CreatedAtUtc, @ReadAtUtc);";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", notification.UserId);
        command.Parameters.AddWithValue("@Title", notification.Title);
        command.Parameters.AddWithValue("@Message", notification.Message);
        command.Parameters.AddWithValue("@Type", notification.Type);
        command.Parameters.AddWithValue("@OpportunityProcessCode", (object?)notification.OpportunityProcessCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@OpportunityTitle", (object?)notification.OpportunityTitle ?? DBNull.Value);
        command.Parameters.AddWithValue("@AffinityScore", (object?)notification.AffinityScore ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsRead", notification.IsRead);
        command.Parameters.AddWithValue("@CreatedAtUtc", notification.CreatedAtUtc);
        command.Parameters.AddWithValue("@ReadAtUtc", (object?)notification.ReadAtUtc ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task<int> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.PanelNotification
            SET IsRead = 1,
                ReadAtUtc = GETUTCDATE()
            WHERE NotificationId = @NotificationId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> MarkAllAsReadByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.PanelNotification
            SET IsRead = 1,
                ReadAtUtc = GETUTCDATE()
            WHERE UserId = @UserId AND IsRead = 0;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT COUNT(*) 
            FROM dbo.PanelNotification
            WHERE UserId = @UserId AND IsRead = 0;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private static PanelNotification MapPanelNotification(SqlDataReader reader)
    {
        return new PanelNotification
        {
            NotificationId = reader.GetInt32(reader.GetOrdinal("NotificationId")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Message = reader.GetString(reader.GetOrdinal("Message")),
            Type = reader.GetString(reader.GetOrdinal("Type")),
            OpportunityProcessCode = reader.IsDBNull(reader.GetOrdinal("OpportunityProcessCode")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("OpportunityProcessCode")),
            OpportunityTitle = reader.IsDBNull(reader.GetOrdinal("OpportunityTitle")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("OpportunityTitle")),
            AffinityScore = reader.IsDBNull(reader.GetOrdinal("AffinityScore")) 
                ? null 
                : reader.GetInt32(reader.GetOrdinal("AffinityScore")),
            IsRead = reader.GetBoolean(reader.GetOrdinal("IsRead")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            ReadAtUtc = reader.IsDBNull(reader.GetOrdinal("ReadAtUtc")) 
                ? null 
                : reader.GetDateTime(reader.GetOrdinal("ReadAtUtc"))
        };
    }
}
