using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fdb.Services.DatabaseProviders;

public class TargetDatabaseItem : ObservableObject
{
    public int? ConnectionId { get; set; }
    public string Key { get; set; } = "sqlite_local";
    public string DatabaseType { get; set; } = "SQLite";
    public string DisplayName { get; set; } = "Local SQLite";
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string DatabaseName { get; set; } = "fdb_studio.db";
    public string AuthType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsLocalSqlite { get; set; } = true;
    public bool IsConnected { get; set; } = true;

    public string BadgeColor => DatabaseType switch
    {
        "MSSQL" => "#2563EB",
        "PostgreSQL" => "#336791",
        "MySQL" => "#00758F",
        "SQLite" => "#003B57",
        _ => "#4B5563"
    };

    public string FullDisplayName => IsLocalSqlite
        ? "Local SQLite (fdb_studio.db)"
        : $"{DatabaseName} [{DatabaseType}] ({Server}:{Port})";

    public override string ToString() => FullDisplayName;
}
