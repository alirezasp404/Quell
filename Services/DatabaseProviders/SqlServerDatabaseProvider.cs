using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Fdb.Services.DatabaseProviders;

public class SqlServerDatabaseProvider : IDatabaseProvider
{
    public string Id => "MSSQL";
    public string DisplayName => "Microsoft SQL Server";
    public string ShortDescription => "Enterprise SQL Server & Azure SQL";
    public int DefaultPort => 1433;
    public string DefaultDatabase => "master";
    public string DefaultUsername => "sa";
    public bool IsFileBased => false;
    public bool SupportsWindowsAuth => true;
    public bool SupportsTrustServerCert => true;

    public string BuildConnectionString(DatabaseConnectionConfig config)
    {
        var builder = new SqlConnectionStringBuilder();

        string server = string.IsNullOrWhiteSpace(config.Server) ? "localhost" : config.Server.Trim();
        int port = config.Port > 0 ? config.Port : 1433;
        if (server.Contains(':'))
        {
            var parts = server.Split(':');
            server = parts[0];
            if (int.TryParse(parts[1], out int p)) port = p;
        }

        if (port != 1433 && !server.Contains(','))
        {
            builder.DataSource = $"{server},{port}";
        }
        else
        {
            builder.DataSource = server;
        }

        builder.InitialCatalog = string.IsNullOrWhiteSpace(config.Database) ? "master" : config.Database.Trim();
        builder.TrustServerCertificate = true; // Always allow self-signed / local dev certificates
        builder.Encrypt = false; // Disable mandatory SSL requirement for standard dev environments
        builder.ConnectTimeout = Math.Clamp(config.TimeoutSeconds, 3, 30);

        if (string.Equals(config.AuthType, "Windows Authentication", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = config.Username ?? "sa";
            builder.Password = config.Password ?? string.Empty;
        }

        return builder.ConnectionString;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string connStr = BuildConnectionString(config);
            await using var conn = new SqlConnection(connStr);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Min(8, config.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await conn.OpenAsync(linkedCts.Token);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @@VERSION;";
            var versionObj = await cmd.ExecuteScalarAsync(linkedCts.Token);
            sw.Stop();

            string versionStr = versionObj?.ToString() ?? "Microsoft SQL Server";
            string firstLine = versionStr.Split('\n')[0].Trim();

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Connected to SQL Server in {sw.ElapsedMilliseconds} ms!",
                ServerVersion = firstLine,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SQL Server Error ({ex.Number}): {ex.Message.Trim()}",
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
                Message = $"Connection timed out after {sw.ElapsedMilliseconds} ms. Ensure the SQL Server instance is running and port {config.Port} is open.",
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
        var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
