using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Quell.Services.DatabaseProviders;

public class TargetDatabaseItem : ObservableObject
{
    public int? ConnectionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = "MSSQL";
    public string DisplayName { get; set; } = string.Empty;
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string DatabaseName { get; set; } = "master";
    public string AuthType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsLocalSqlite { get; set; } = false;
    public bool IsConnected { get; set; } = false;

    public string BadgeColor => DatabaseType switch
    {
        "MSSQL" => "#2563EB",
        "PostgreSQL" => "#336791",
        "MySQL" => "#00758F",
        "SQLite" => "#003B57",
        _ => "#4B5563"
    };

    public string FullDisplayName => IsLocalSqlite
        ? "Local SQLite"
        : (DatabaseType == "SQLite" 
            ? DisplayName 
            : (!string.IsNullOrWhiteSpace(DatabaseName) ? DatabaseName : Server));

    public override string ToString() => FullDisplayName;
}
