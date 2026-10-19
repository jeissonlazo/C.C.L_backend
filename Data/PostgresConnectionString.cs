using Npgsql;

namespace gestion_inventario.Data;

public static class PostgresConnectionString
{
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
            throw new InvalidOperationException("La URL de PostgreSQL debe incluir usuario y contraseña.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };

        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]).ToLowerInvariant();
            var setting = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : "";
            switch (key)
            {
                case "sslmode": builder["SSL Mode"] = setting; break;
                case "channel_binding": builder["Channel Binding"] = setting; break;
                case "connect_timeout": builder["Timeout"] = setting; break;
                case "application_name": builder["Application Name"] = setting; break;
                default: throw new InvalidOperationException($"La opción '{key}' de la URL PostgreSQL no está soportada.");
            }
        }

        return builder.ConnectionString;
    }
}
