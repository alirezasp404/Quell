using System;
using System.Collections.Generic;

namespace Quell.Services.DatabaseProviders;

public class DatabaseProviderRegistry
{
    private static DatabaseProviderRegistry? _instance;
    public static DatabaseProviderRegistry Instance => _instance ??= new DatabaseProviderRegistry();

    private readonly Dictionary<DatabaseType, IDatabaseProvider> _providers = new();

    public DatabaseProviderRegistry()
    {
        Register(new SqlServerDatabaseProvider());
        Register(new PostgreSqlDatabaseProvider());
        Register(new MySqlDatabaseProvider());
        Register(new SqliteDatabaseProvider());
    }

    public void Register(IDatabaseProvider provider)
    {
        _providers[provider.DatabaseType] = provider;
    }

    public IDatabaseProvider GetProvider(DatabaseType dbType)
    {
        if (_providers.TryGetValue(dbType, out var provider))
        {
            return provider;
        }
        return _providers[DatabaseType.MSSQL];
    }

    public IDatabaseProvider GetProvider(string id)
    {
        return GetProvider(DatabaseTypeExtensions.Parse(id));
    }

    public IReadOnlyCollection<IDatabaseProvider> GetAllProviders() => _providers.Values;
}
