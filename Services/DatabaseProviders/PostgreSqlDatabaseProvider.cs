using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Quell.Services.DatabaseProviders;

public class PostgreSqlDatabaseProvider : IDatabaseProvider
{
    public string Id => "PostgreSQL";
    public string DisplayName => "PostgreSQL";
    public string ShortDescription => "Open source object-relational database";
    public int DefaultPort => 5432;
    public string DefaultDatabase => "postgres";
    public string DefaultUsername => "postgres";
    public bool IsFileBased => false;
    public bool SupportsWindowsAuth => false;
    public bool SupportsTrustServerCert => false;

    public string BuildConnectionString(DatabaseConnectionConfig config)
    {
        string host = string.IsNullOrWhiteSpace(config.Server) ? "localhost" : config.Server.Trim();
        int port = config.Port > 0 ? config.Port : 5432;
        if (host.Contains(':'))
        {
            var parts = host.Split(':');
            host = parts[0];
            if (int.TryParse(parts[1], out int p)) port = p;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = string.IsNullOrWhiteSpace(config.Database) ? "postgres" : config.Database.Trim(),
            Username = string.IsNullOrWhiteSpace(config.Username) ? "postgres" : config.Username.Trim(),
            Password = config.Password ?? string.Empty,
            Timeout = Math.Clamp(config.TimeoutSeconds, 3, 30)
        };
        return builder.ConnectionString;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string connStr = BuildConnectionString(config);
            await using var conn = new NpgsqlConnection(connStr);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(8, config.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await conn.OpenAsync(linkedCts.Token);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT version();";
            var ver = await cmd.ExecuteScalarAsync(linkedCts.Token);
            sw.Stop();

            string versionStr = ver?.ToString() ?? "PostgreSQL";
            string firstPart = versionStr.Split(',')[0].Trim();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Connected to PostgreSQL in {sw.ElapsedMilliseconds} ms!",
                ServerVersion = firstPart,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (NpgsqlException ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"PostgreSQL Error: {ex.Message.Trim()}",
                LatencyMs = sw.ElapsedMilliseconds,
                Exception = ex
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection timed out after {sw.ElapsedMilliseconds} ms. Ensure PostgreSQL is running on port {config.Port}.",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection failed ({sw.ElapsedMilliseconds} ms): {ex.Message.Trim()}",
                LatencyMs = sw.ElapsedMilliseconds,
                Exception = ex
            };
        }
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        string connStr = BuildConnectionString(config);
        var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
