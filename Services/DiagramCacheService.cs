using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Quell.ViewModels;

namespace Quell.Services;

/// <summary>
/// Cached snapshot of an analyzed database ER diagram.
/// </summary>
public class CachedDiagramData
{
    public string DatabaseKey { get; set; } = string.Empty;
    public string DatabaseTitle { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.Now;
    public List<DiagramTableItem> Tables { get; set; } = new();
    public HashSet<string> KnownTableNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int TableCount => Tables.Count;
}

/// <summary>
/// High-performance in-memory cache service for Database ER Diagrams.
/// Caches analyzed schema tables, columns, primary keys, foreign keys, and visual coordinates
/// per database, ensuring instantaneous subsequent diagram loads and supporting automatic
/// or manual invalidation when tables are added, modified, or dropped.
/// </summary>
public class DiagramCacheService
{
    private static readonly Lazy<DiagramCacheService> _instance = new(() => new DiagramCacheService());
    public static DiagramCacheService Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, CachedDiagramData> _cache = new(StringComparer.OrdinalIgnoreCase);

    private DiagramCacheService()
    {
    }

    /// <summary>
    /// Attempts to retrieve cached diagram data for the given database key.
    /// </summary>
    public bool TryGet(string databaseKey, out CachedDiagramData? data)
    {
        if (string.IsNullOrWhiteSpace(databaseKey))
        {
            data = null;
            return false;
        }

        return _cache.TryGetValue(databaseKey, out data);
    }

    /// <summary>
    /// Checks whether valid cached diagram data exists for the given database.
    /// </summary>
    public bool HasCache(string databaseKey)
    {
        return !string.IsNullOrWhiteSpace(databaseKey) && _cache.ContainsKey(databaseKey);
    }

    /// <summary>
    /// Stores or updates analyzed diagram tables for the specified database key.
    /// Clones items to preserve snapshot integrity.
    /// </summary>
    public void Store(string databaseKey, string databaseTitle, string databaseType, IEnumerable<DiagramTableItem> tables)
    {
        if (string.IsNullOrWhiteSpace(databaseKey)) return;

        var tableList = tables.Select(t => t.Clone()).ToList();
        var knownNames = new HashSet<string>(tableList.Select(t => t.TableName), StringComparer.OrdinalIgnoreCase);

        var data = new CachedDiagramData
        {
            DatabaseKey = databaseKey,
            DatabaseTitle = databaseTitle,
            DatabaseType = databaseType,
            CachedAt = DateTime.Now,
            Tables = tableList,
            KnownTableNames = knownNames
        };

        _cache[databaseKey] = data;
    }

    /// <summary>
    /// Synchronizes updated table positions (X, Y) back into the cache so customized user layouts are preserved.
    /// </summary>
    public void UpdateTablePositions(string databaseKey, IEnumerable<DiagramTableItem> tables)
    {
        if (string.IsNullOrWhiteSpace(databaseKey) || !_cache.TryGetValue(databaseKey, out var cachedData))
        {
            return;
        }

        foreach (var liveTable in tables)
        {
            var match = cachedData.Tables.FirstOrDefault(t => t.TableName.Equals(liveTable.TableName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.X = liveTable.X;
                match.Y = liveTable.Y;
                match.Width = liveTable.Width;
                match.Height = liveTable.Height;
            }
        }
    }

    /// <summary>
    /// Invalidates and clears the cached diagram for a specific database.
    /// </summary>
    public bool Invalidate(string databaseKey)
    {
        if (string.IsNullOrWhiteSpace(databaseKey)) return false;
        return _cache.TryRemove(databaseKey, out _);
    }

    /// <summary>
    /// Clears all cached diagrams across all databases.
    /// </summary>
    public void ClearAll()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Checks if the cached schema is stale compared to the currently discovered table names.
    /// Returns true if tables count changed or any new table is found.
    /// </summary>
    public bool IsStale(string databaseKey, IEnumerable<string> currentTableNames)
    {
        if (!_cache.TryGetValue(databaseKey, out var cached))
        {
            return true;
        }

        var currentList = currentTableNames.ToList();
        if (currentList.Count != cached.TableCount)
        {
            return true;
        }

        foreach (var name in currentList)
        {
            if (!cached.KnownTableNames.Contains(name))
            {
                return true;
            }
        }

        return false;
    }
}
