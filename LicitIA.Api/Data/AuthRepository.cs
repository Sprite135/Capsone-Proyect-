using LicitIA.Api.Contracts;
using LicitIA.Api.Models;
using LicitIA.Api.Security;
using System.Data.SqlClient;

namespace LicitIA.Api.Data;

public sealed class AuthRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly PasswordService _passwordService;

    public AuthRepository(SqlConnectionFactory connectionFactory, PasswordService passwordService)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
    }

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (1)
                UserId,
                FullName,
                CompanyName,
                Email,
                RoleName,
                PasswordHash,
                PasswordSalt,
                IsActive,
                CreatedAtUtc
            FROM dbo.AppUsers
            WHERE Email = @Email
              AND IsActive = 1;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", NormalizeEmail(email));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapUser(reader);
    }

    public async Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (1)
                UserId,
                FullName,
                CompanyName,
                Email,
                RoleName,
                PasswordHash,
                PasswordSalt,
                IsActive,
                CreatedAtUtc
            FROM dbo.AppUsers
            WHERE UserId = @UserId
              AND IsActive = 1;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapUser(reader);
    }

    public async Task<AppUser> CreateUserAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var (passwordHash, passwordSalt) = _passwordService.HashPassword(request.Password);
        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Email = NormalizeEmail(request.Email),
            RoleName = request.Role.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.AppUsers
            (
                UserId,
                FullName,
                CompanyName,
                Email,
                RoleName,
                PasswordHash,
                PasswordSalt,
                IsActive,
                CreatedAtUtc
            )
            VALUES
            (
                @UserId,
                @FullName,
                @CompanyName,
                @Email,
                @RoleName,
                @PasswordHash,
                @PasswordSalt,
                @IsActive,
                @CreatedAtUtc
            );
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", user.UserId);
        command.Parameters.AddWithValue("@FullName", user.FullName);
        command.Parameters.AddWithValue("@CompanyName", user.CompanyName);
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@RoleName", user.RoleName);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@PasswordSalt", user.PasswordSalt);
        command.Parameters.AddWithValue("@IsActive", user.IsActive);
        command.Parameters.AddWithValue("@CreatedAtUtc", user.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return user;
    }

    public async Task UpdateUserProfileAsync(string email, string companyName, string role, string? phone, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // Note: Phone column doesn't exist in current DB schema, so we only update CompanyName and RoleName
        const string sql = """
            UPDATE dbo.AppUsers
            SET
                CompanyName = @CompanyName,
                RoleName = @RoleName
            WHERE Email = @Email
              AND IsActive = 1;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", NormalizeEmail(email));
        command.Parameters.AddWithValue("@CompanyName", companyName.Trim());
        command.Parameters.AddWithValue("@RoleName", role.Trim());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SavePasswordResetTokenAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.AppUsers
            SET
                PasswordResetToken = @PasswordResetToken,
                PasswordResetTokenExpiryUtc = @PasswordResetTokenExpiryUtc
            WHERE UserId = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PasswordResetToken", token);
        command.Parameters.AddWithValue("@PasswordResetTokenExpiryUtc", DateTime.UtcNow.AddHours(1));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AppUser?> GetUserByResetTokenAsync(string token, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (1)
                UserId,
                FullName,
                CompanyName,
                Email,
                RoleName,
                PasswordHash,
                PasswordSalt,
                IsActive,
                CreatedAtUtc
            FROM dbo.AppUsers
            WHERE PasswordResetToken = @PasswordResetToken
              AND PasswordResetTokenExpiryUtc > GETUTCDATE()
              AND IsActive = 1;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PasswordResetToken", token);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapUser(reader);
    }

    public async Task UpdatePasswordAsync(Guid userId, byte[] passwordHash, byte[] passwordSalt, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.AppUsers
            SET
                PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt
            WHERE UserId = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@PasswordSalt", passwordSalt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearPasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.AppUsers
            SET
                PasswordResetToken = NULL,
                PasswordResetTokenExpiryUtc = NULL
            WHERE UserId = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordLoginAttemptAsync(string email, bool success, string? ipAddress, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.LoginAttempts
            (
                Email,
                AttemptTimeUtc,
                Success,
                IpAddress
            )
            VALUES
            (
                @Email,
                @AttemptTimeUtc,
                @Success,
                @IpAddress
            );
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", NormalizeEmail(email));
        command.Parameters.AddWithValue("@AttemptTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@Success", success);
        command.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetFailedLoginAttemptsAsync(string email, TimeSpan timeWindow, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT COUNT(*)
            FROM dbo.LoginAttempts
            WHERE Email = @Email
              AND Success = 0
              AND AttemptTimeUtc > @CutoffTimeUtc;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", NormalizeEmail(email));
        command.Parameters.AddWithValue("@CutoffTimeUtc", DateTime.UtcNow.Subtract(timeWindow));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task CleanupOldLoginAttemptsAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            DELETE FROM dbo.LoginAttempts
            WHERE AttemptTimeUtc < @CutoffTimeUtc;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CutoffTimeUtc", DateTime.UtcNow.AddDays(-30));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AppUser MapUser(SqlDataReader reader) =>
        new()
        {
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            FullName = reader.GetString(reader.GetOrdinal("FullName")),
            CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
            PasswordHash = (byte[])reader["PasswordHash"],
            PasswordSalt = (byte[])reader["PasswordSalt"],
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
