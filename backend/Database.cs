namespace MovieNight;

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
        ConnectionString =
            $"Server={host};Port={port};Database={database};User={username};Password={password};SslMode=Required;AllowPublicKeyRetrieval=True;";
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
                var colon = uri.UserInfo.IndexOf(':');
                user = colon >= 0
                    ? Uri.UnescapeDataString(uri.UserInfo[..colon])
                    : Uri.UnescapeDataString(uri.UserInfo);
                pass = colon >= 0 ? Uri.UnescapeDataString(uri.UserInfo[(colon + 1)..]) : "";
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
