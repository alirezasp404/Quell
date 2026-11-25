using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Quell.Services.DatabaseProviders;

public class TargetDatabaseItem : ObservableObject
{
    public int? ConnectionId { get; set; }
    public string Key { get; set; } = string.Empty;

    private DatabaseType _databaseType = DatabaseType.MSSQL;
    public DatabaseType DatabaseType
    {
        get => _databaseType;
        set
        {
            if (SetProperty(ref _databaseType, value))
            {
                OnPropertyChanged(nameof(BadgeColor));
                OnPropertyChanged(nameof(DialectTag));
                OnPropertyChanged(nameof(ServerSummary));
                OnPropertyChanged(nameof(FullDisplayName));
            }
        }
    }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(FullDisplayName));
            }
        }
    }

    private string _server = "localhost";
    public string Server
    {
        get => _server;
        set
        {
            if (SetProperty(ref _server, value))
            {
                OnPropertyChanged(nameof(ServerSummary));
                OnPropertyChanged(nameof(FullDisplayName));
            }
        }
    }

    private int _port = 1433;
    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                OnPropertyChanged(nameof(ServerSummary));
            }
        }
    }

    private string _databaseName = "master";
    public string DatabaseName
    {
        get => _databaseName;
        set
        {
            if (SetProperty(ref _databaseName, value))
            {
                OnPropertyChanged(nameof(FullDisplayName));
            }
        }
    }

    public string AuthType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsLocalSqlite { get; set; } = false;

    private bool _isConnected = false;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string BadgeColor => DatabaseType.GetBadgeColor();

    public string DialectTag => DatabaseType.ToDialectString();

    public string ServerSummary => DatabaseType == DatabaseType.SQLite
        ? (string.IsNullOrWhiteSpace(Server) || Server == ":memory:" ? "SQLite" : System.IO.Path.GetFileName(Server))
        : (Port > 0 ? $"{Server}:{Port}" : Server);

    public string FullDisplayName => IsLocalSqlite
        ? "Local SQLite"
        : (DatabaseType == DatabaseType.SQLite 
            ? DisplayName 
            : (!string.IsNullOrWhiteSpace(DatabaseName) ? DatabaseName : Server));

    public override string ToString() => FullDisplayName;
}

public class DatabaseSwitcherDbItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }
}
