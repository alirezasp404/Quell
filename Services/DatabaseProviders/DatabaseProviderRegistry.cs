using System;
using System.Collections.Generic;

namespace Quell.Services.DatabaseProviders;

public class DatabaseProviderRegistry
{
    private static DatabaseProviderRegistry? _instance;
    public static DatabaseProviderRegistry Instance => _instance ??= new DatabaseProviderRegistry();

    private readonly Dictionary<string, IDatabaseProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseProviderRegistry()
    {
        Register(new SqlServerDatabaseProvider());
        Register(new PostgreSqlDatabaseProvider());
        Register(new MySqlDatabaseProvider());
        Register(new SqliteDatabaseProvider());
    }

    public void Register(IDatabaseProvider provider)
    {
        _providers[provider.Id] = provider;
    }

    public IDatabaseProvider GetProvider(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _providers.TryGetValue(id, out var provider))
        {
            return provider;
        }
        return _providers["MSSQL"];
    }

    public IReadOnlyCollection<IDatabaseProvider> GetAllProviders() => _providers.Values;
}
