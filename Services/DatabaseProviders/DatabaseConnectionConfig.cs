using System;
using System.IO;

namespace Fdb.Services.DatabaseProviders;

public record DatabaseConnectionConfig
{
    public int Id { get; init; } = 0;
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

    public string ConnectionKey => Id > 0
        ? $"id_{Id}"
        : DatabaseType == "SQLite"
            ? $"sqlite_{FilePath}"
            : $"{DatabaseType}_{Server}_{Port}_{Database}";

    public string DisplayName => DatabaseType == "SQLite"
        ? (string.IsNullOrEmpty(Path.GetFileName(FilePath)) ? ":memory:" : Path.GetFileName(FilePath))
        : (!string.IsNullOrWhiteSpace(Database) ? Database : Server);
}
