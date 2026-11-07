using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Quell.Services.DatabaseProviders;

public interface IDatabaseProvider
{
    string Id { get; }
    string DisplayName { get; }
    string ShortDescription { get; }
    int DefaultPort { get; }
    string DefaultDatabase { get; }
    string DefaultUsername { get; }
    bool IsFileBased { get; }
    bool SupportsWindowsAuth { get; }
    bool SupportsTrustServerCert { get; }

    string BuildConnectionString(DatabaseConnectionConfig config);
    Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default);
    Task<IDbConnection> CreateOpenConnectionAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default);
}
