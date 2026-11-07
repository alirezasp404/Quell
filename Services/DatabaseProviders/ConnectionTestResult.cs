using System;

namespace Quell.Services.DatabaseProviders;

public record ConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ServerVersion { get; init; }
    public long LatencyMs { get; init; }
    public Exception? Exception { get; init; }
}
