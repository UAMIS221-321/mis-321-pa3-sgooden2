using MySqlConnector;

namespace MovieNight.Services;

public abstract class BaseService
{
    private readonly string _connectionString;

    protected BaseService(IConfiguration configuration)
    {
        _connectionString = Database.GetConnectionString(configuration);
    }

    protected MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);
}
