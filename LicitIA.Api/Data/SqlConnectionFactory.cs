using System.Data.SqlClient;

namespace LicitIA.Api.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetSection("Database")["ConnectionString"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se encontro la cadena de conexion para SQL Server.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
