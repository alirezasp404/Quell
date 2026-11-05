using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Quell.Services.DatabaseProviders;

public class MySqlDatabaseProvider : IDatabaseProvider
{
    public string Id => "MySQL";
    public string DisplayName => "MySQL / MariaDB";
    public string ShortDescription => "Popular relational database engine";
    public int DefaultPort => 3306;
    public string DefaultDatabase => "mysql";
    public string DefaultUsername => "root";
    public bool IsFileBased => false;
    public bool SupportsWindowsAuth => false;
    public bool SupportsTrustServerCert => false;

    public string BuildConnectionString(DatabaseConnectionConfig config)
    {
        string server = string.IsNullOrWhiteSpace(config.Server) ? "localhost" : config.Server.Trim();
        uint port = (uint)(config.Port > 0 ? config.Port : 3306);
        if (server.Contains(':'))
        {
            var parts = server.Split(':');
            server = parts[0];
            if (uint.TryParse(parts[1], out uint p)) port = p;
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = server,
            Port = port,
            Database = string.IsNullOrWhiteSpace(config.Database) ? "mysql" : config.Database.Trim(),
            UserID = string.IsNullOrWhiteSpace(config.Username) ? "root" : config.Username.Trim(),
            Password = config.Password ?? string.Empty,
            ConnectionTimeout = (uint)Math.Clamp(config.TimeoutSeconds, 3, 30)
        };
        return builder.ConnectionString;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string connStr = BuildConnectionString(config);
            await using var conn = new MySqlConnection(connStr);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(8, config.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await conn.OpenAsync(linkedCts.Token);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT VERSION();";
            var ver = await cmd.ExecuteScalarAsync(linkedCts.Token);
            sw.Stop();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Connected to MySQL in {sw.ElapsedMilliseconds} ms!",
                ServerVersion = $"MySQL {ver}",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (MySqlException ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"MySQL Error ({ex.Number}): {ex.Message.Trim()}",
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
                Message = $"Connection timed out after {sw.ElapsedMilliseconds} ms. Ensure MySQL is running on port {config.Port}.",
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
        var conn = new MySqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
