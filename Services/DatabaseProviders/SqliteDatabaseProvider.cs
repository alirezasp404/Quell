using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Fdb.Services.DatabaseProviders;

public class SqliteDatabaseProvider : IDatabaseProvider
{
    public string Id => "SQLite";
    public string DisplayName => "SQLite";
    public string ShortDescription => "Embedded local database file or memory";
    public int DefaultPort => 0;
    public string DefaultDatabase => "main";
    public string DefaultUsername => "";
    public bool IsFileBased => true;
    public bool SupportsWindowsAuth => false;
    public bool SupportsTrustServerCert => false;

    public string BuildConnectionString(DatabaseConnectionConfig config)
    {
        var builder = new SqliteConnectionStringBuilder();
        string source = string.IsNullOrWhiteSpace(config.FilePath) ? ":memory:" : config.FilePath.Trim();
        builder.DataSource = source;
        if (!string.IsNullOrWhiteSpace(config.Password))
        {
            builder.Password = config.Password;
        }
        return builder.ConnectionString;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string source = string.IsNullOrWhiteSpace(config.FilePath) ? ":memory:" : config.FilePath.Trim();
            if (source != ":memory:" && !File.Exists(source))
            {
                // Verify directory exists or can be created
                string? dir = Path.GetDirectoryName(source);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    return new ConnectionTestResult
                    {
                        Success = false,
                        Message = $"Directory does not exist: {dir}",
                        LatencyMs = 0
                    };
                }
            }

            string connStr = BuildConnectionString(config);
            await using var conn = new SqliteConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sqlite_version();";
            var ver = await cmd.ExecuteScalarAsync(cancellationToken);
            sw.Stop();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Connected to SQLite database in {sw.ElapsedMilliseconds} ms!",
                ServerVersion = $"SQLite v{ver}",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SQLite error: {ex.Message.Trim()}",
                LatencyMs = sw.ElapsedMilliseconds,
                Exception = ex
            };
        }
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        string connStr = BuildConnectionString(config);
        var conn = new SqliteConnection(connStr);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
