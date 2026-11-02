using System;

namespace Fdb.Services.DatabaseProviders;

public record DatabaseConnectionConfig
{
    public string DatabaseType { get; init; } = "MSSQL";
    public string Server { get; init; } = "localhost";
    public int Port { get; init; } = 1433;
    public string Database { get; init; } = "master";
    public string AuthType { get; init; } = "SQL Server Authentication";
    public string Username { get; init; } = "sa";
    public string Password { get; init; } = string.Empty;
    public bool TrustServerCertificate { get; init; } = true;
    public string FilePath { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 15;
}
