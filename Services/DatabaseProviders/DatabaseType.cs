using System;

namespace Quell.Services.DatabaseProviders;

public enum DatabaseType
{
    SQLite,
    PostgreSQL,
    MySQL,
    MSSQL
}

public static class DatabaseTypeExtensions
{
    public static DatabaseType Parse(string? value, DatabaseType defaultValue = DatabaseType.MSSQL)
    {
        if (TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public static bool TryParse(string? value, out DatabaseType result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = DatabaseType.MSSQL;
            return false;
        }

        switch (value.Trim().ToUpperInvariant())
        {
            case "MSSQL":
            case "SQLSERVER":
            case "SQL SERVER":
                result = DatabaseType.MSSQL;
                return true;
            case "POSTGRESQL":
            case "POSTGRES":
            case "PG":
                result = DatabaseType.PostgreSQL;
                return true;
            case "MYSQL":
            case "MARIADB":
                result = DatabaseType.MySQL;
                return true;
            case "SQLITE":
                result = DatabaseType.SQLite;
                return true;
            default:
                result = DatabaseType.MSSQL;
                return false;
        }
    }

    public static string GetBadgeColor(this DatabaseType dbType) => dbType switch
    {
        DatabaseType.MSSQL => "#2563EB",
        DatabaseType.PostgreSQL => "#336791",
        DatabaseType.MySQL => "#00758F",
        DatabaseType.SQLite => "#003B57",
        _ => "#4B5563"
    };

    public static string ToDialectString(this DatabaseType dbType) => dbType switch
    {
        DatabaseType.MSSQL => "MSSQL",
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.MySQL => "MySQL",
        DatabaseType.SQLite => "SQLite",
        _ => "MSSQL"
    };
}
