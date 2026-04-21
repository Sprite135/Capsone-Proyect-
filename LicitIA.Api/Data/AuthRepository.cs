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
