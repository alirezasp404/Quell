using System;
using System.Collections.Generic;
using System.IO;
using Quell.Models;
using Quell.Services.DatabaseProviders;

namespace Quell.Services.Terminal;

public class DatabaseCliService
{
    private static DatabaseCliService? _instance;
    public static DatabaseCliService Instance => _instance ??= new DatabaseCliService();

    public CliLaunchInfo ResolveCli(DatabaseConnectionConfig config, TerminalSettingsModel? settings = null)
    {
        var info = new CliLaunchInfo();

        switch (config.DatabaseType)
        {
            case DatabaseType.PostgreSQL:
                ConfigurePostgres(config, settings?.PsqlPath, info);
                break;

            case DatabaseType.MySQL:
                ConfigureMySql(config, settings?.MySqlPath, info);
                break;

            case DatabaseType.SQLite:
                ConfigureSqlite(config, settings?.SqlitePath, info);
                break;

            case DatabaseType.MSSQL:
                ConfigureSqlServer(config, settings?.SqlCmdPath, info);
                break;

            default:
                info.IsAvailable = false;
                info.MissingToolName = "CLI for " + config.DatabaseType;
                info.Instructions = "Dedicated CLI not supported for this database type.";
                break;
        }

        // Add standard database environment variables
        PopulateEnvironmentVariables(config, info.EnvironmentVariables);

        return info;
    }

    public Dictionary<string, string> BuildConnectionEnvironment(DatabaseConnectionConfig? config)
    {
        var env = new Dictionary<string, string>();
        if (config != null)
        {
            PopulateEnvironmentVariables(config, env);
        }
        return env;
    }

    private static void PopulateEnvironmentVariables(DatabaseConnectionConfig config, Dictionary<string, string> env)
    {
        var dbName = !string.IsNullOrEmpty(config.Database) ? config.Database : config.DisplayName;
        env["FDB_CONNECTED"] = "true";
        env["FDB_DB_TYPE"] = config.DatabaseType.ToDialectString();
        env["FDB_DB_HOST"] = config.Server ?? "localhost";
        env["FDB_DB_PORT"] = config.Port.ToString();
        env["FDB_DB_NAME"] = dbName;
        env["FDB_DB_USER"] = config.Username ?? string.Empty;

        var scheme = config.DatabaseType switch
        {
            DatabaseType.PostgreSQL => "postgresql",
            DatabaseType.MySQL => "mysql",
            DatabaseType.MSSQL => "sqlserver",
            DatabaseType.SQLite => "sqlite",
            _ => "db"
        };

        if (config.DatabaseType == DatabaseType.SQLite)
        {
            var path = !string.IsNullOrEmpty(config.FilePath) ? config.FilePath : config.Database;
            env["DATABASE_URL"] = $"sqlite:///{path}";
        }
        else
        {
            var userPart = !string.IsNullOrEmpty(config.Username)
                ? (string.IsNullOrEmpty(config.Password) ? config.Username : $"{config.Username}:{config.Password}") + "@"
                : string.Empty;

            env["DATABASE_URL"] = $"{scheme}://{userPart}{config.Server}:{config.Port}/{dbName}";
        }
    }

    private static void ConfigurePostgres(DatabaseConnectionConfig config, string? customPath, CliLaunchInfo info)
    {
        if (!string.IsNullOrEmpty(config.Password))
        {
            info.EnvironmentVariables["PGPASSWORD"] = config.Password;
        }

        var exec = FindExecutable(customPath, "psql", @"C:\Program Files\PostgreSQL\17\bin\psql.exe", @"C:\Program Files\PostgreSQL\16\bin\psql.exe", @"C:\Program Files\PostgreSQL\15\bin\psql.exe");
        if (string.IsNullOrEmpty(exec))
        {
            info.IsAvailable = false;
            info.MissingToolName = "PostgreSQL CLI (psql)";
            info.DownloadUrl = "https://www.postgresql.org/download/";
            info.Instructions = "psql was not found in your system PATH. Please install PostgreSQL client tools or configure the executable path in Settings.";
            return;
        }

        info.IsAvailable = true;
        info.ExecutablePath = exec;
        var targetDb = !string.IsNullOrEmpty(config.Database) ? config.Database : "postgres";
        info.Arguments.AddRange(new[]
        {
            "-h", config.Server ?? "localhost",
            "-p", config.Port.ToString(),
            "-U", string.IsNullOrEmpty(config.Username) ? "postgres" : config.Username,
            "-d", targetDb
        });
    }

    private static void ConfigureMySql(DatabaseConnectionConfig config, string? customPath, CliLaunchInfo info)
    {
        if (!string.IsNullOrEmpty(config.Password))
        {
            info.EnvironmentVariables["MYSQL_PWD"] = config.Password;
        }

        var exec = FindExecutable(customPath, "mysql", @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe", @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe");
        if (string.IsNullOrEmpty(exec))
        {
            info.IsAvailable = false;
            info.MissingToolName = "MySQL CLI (mysql)";
            info.DownloadUrl = "https://dev.mysql.com/downloads/";
            info.Instructions = "mysql was not found in your system PATH. Please install MySQL client tools or configure the executable path in Settings.";
            return;
        }

        info.IsAvailable = true;
        info.ExecutablePath = exec;
        var targetDb = !string.IsNullOrEmpty(config.Database) ? config.Database : "mysql";
        info.Arguments.AddRange(new[]
        {
            "-h", config.Server ?? "localhost",
            "-P", config.Port.ToString(),
            "-u", string.IsNullOrEmpty(config.Username) ? "root" : config.Username,
            "-D", targetDb
        });
    }

    private static void ConfigureSqlite(DatabaseConnectionConfig config, string? customPath, CliLaunchInfo info)
    {
        var exec = FindExecutable(customPath, "sqlite3");
        if (string.IsNullOrEmpty(exec))
        {
            info.IsAvailable = false;
            info.MissingToolName = "SQLite CLI (sqlite3)";
            info.DownloadUrl = "https://www.sqlite.org/download.html";
            info.Instructions = "sqlite3 was not found in your system PATH. Please download the SQLite command-line shell or configure the path in Settings.";
            return;
        }

        info.IsAvailable = true;
        info.ExecutablePath = exec;
        var targetPath = !string.IsNullOrEmpty(config.FilePath) ? config.FilePath : config.Database;
        if (!string.IsNullOrEmpty(targetPath))
        {
            info.Arguments.Add(targetPath);
        }
    }

    private static void ConfigureSqlServer(DatabaseConnectionConfig config, string? customPath, CliLaunchInfo info)
    {
        var exec = FindExecutable(customPath, "sqlcmd", @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe");
        if (string.IsNullOrEmpty(exec))
        {
            info.IsAvailable = false;
            info.MissingToolName = "SQL Server CLI (sqlcmd)";
            info.DownloadUrl = "https://learn.microsoft.com/sql/tools/sqlcmd/";
            info.Instructions = "sqlcmd was not found in your system PATH. Please install SQL Server command-line utilities or configure the path in Settings.";
            return;
        }

        info.IsAvailable = true;
        info.ExecutablePath = exec;
        var targetDb = !string.IsNullOrEmpty(config.Database) ? config.Database : "master";
        info.Arguments.AddRange(new[]
        {
            "-S", $"{config.Server},{config.Port}",
            "-d", targetDb
        });

        if (string.Equals(config.AuthType, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            info.Arguments.Add("-E");
        }
        else
        {
            if (!string.IsNullOrEmpty(config.Username))
            {
                info.Arguments.AddRange(new[] { "-U", config.Username });
            }
            if (!string.IsNullOrEmpty(config.Password))
            {
                info.Arguments.AddRange(new[] { "-P", config.Password });
            }
        }
    }

    private static string? FindExecutable(string? customPath, string defaultBinary, params string[] commonLocations)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        var inPath = ShellDetectorService.FindInPath(defaultBinary);
        if (!string.IsNullOrEmpty(inPath))
        {
            return inPath;
        }

        foreach (var loc in commonLocations)
        {
            if (File.Exists(loc))
            {
                return loc;
            }
        }

        return null;
    }
}
