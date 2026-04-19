namespace MovieNight;

using MySqlConnector;

public class Database
{
    public string Host { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Port { get; set; } = "3306";
    public string Password { get; set; } = "";
    public string ConnectionString { get; set; } = "";

    public Database(string host, string database, string username, string port, string password)
    {
        Host = host;
        DatabaseName = database;
        Username = username;
        Port = port;
        Password = password;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.TryParse(port, out var parsedPort) ? parsedPort : 3306,
            Database = database,
            UserID = username,
            Password = password,
            // JawsDB plans vary; Preferred works for SSL and non-SSL hosts.
            SslMode = MySqlSslMode.Preferred,
            AllowPublicKeyRetrieval = true
        };
        ConnectionString = builder.ConnectionString;
    }

    /// <summary>
    /// Connection resolution: JAWSDB_URL -> ConnectionStrings:DefaultConnection -> local MySQL
    /// </summary>
    public static string GetConnectionString(IConfiguration configuration)
    {
        var jawsDbUrl = Environment.GetEnvironmentVariable("JAWSDB_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(jawsDbUrl))
        {
            var db = FromUrl(jawsDbUrl);
            if (db != null) return db.ConnectionString;
        }

        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var envPassword = Environment.GetEnvironmentVariable("MOVIENIGHT_DB_PASSWORD");
        var configPassword = configuration["MySql:Password"];
        var password = !string.IsNullOrEmpty(envPassword) ? envPassword : (configPassword ?? "");

        return $"Server=localhost;Port=3306;Database=MovieNight;User=root;Password={password};SslMode=None;";
    }

    public static Database? FromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var user = "";
            var pass = "";
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                user = Uri.UnescapeDataString(parts[0]);
                pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }
            var db = uri.AbsolutePath?.TrimStart('/') ?? "";
            var port = uri.Port > 0 ? uri.Port.ToString() : "3306";
            return new Database(uri.Host ?? "", db, user, port, pass);
        }
        catch
        {
            return null;
        }
    }
}
