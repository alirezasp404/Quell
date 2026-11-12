using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Quell.Services.DatabaseProviders;

namespace Quell.Services;

public class SqlSnippet
{
    public string Keyword { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SqlKeywordsService
{
    private static SqlKeywordsService? _instance;
    public static SqlKeywordsService Instance => _instance ??= new SqlKeywordsService();

    public DatabaseType CurrentDialect { get; private set; } = DatabaseType.MSSQL;
    public List<string> Keywords { get; private set; } = new();
    public List<SqlSnippet> Snippets { get; private set; } = new();
    public List<TableMetadata> ActiveTables { get; private set; } = new();
    public string ActiveDatabaseName { get; private set; } = string.Empty;
    public Regex? KeywordRegex { get; private set; }

    public void UpdateActiveTables(string? databaseName, IEnumerable<TableMetadata>? tables)
    {
        ActiveDatabaseName = databaseName ?? string.Empty;
        ActiveTables = tables != null ? tables.ToList() : new List<TableMetadata>();
    }

    public void ClearActiveTables()
    {
        ActiveDatabaseName = string.Empty;
        ActiveTables.Clear();
    }

    public SqlKeywordsService()
    {
        // 1. Initial fast fallback keywords (for zero-latency UI initialization)
        LoadDefaultFallbackKeywords();
    }

    private void LoadDefaultFallbackKeywords()
    {
        Keywords = new List<string>
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
            "CREATE", "ALTER", "DROP", "TRUNCATE", "TABLE", "VIEW", "INDEX", "DATABASE",
            "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "ON", "GROUP", "ORDER", "BY",
            "HAVING", "ASC", "DESC", "TOP", "DISTINCT", "UNION", "ALL", "AND", "OR", "NOT",
            "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL", "AS", "CASE", "WHEN", "THEN", "ELSE", "END",
            "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "CONSTRAINT", "DEFAULT", "COUNT", "SUM", "AVG",
            "MIN", "MAX", "INT", "BIGINT", "VARCHAR", "NVARCHAR", "BIT", "DATETIME2"
        };
        BuildRegex();
    }

    /// <summary>
    /// Loads keywords and snippets directly from SQLite database for the specified dialect.
    /// Supports: MSSQL, PostgreSQL, MySQL, SQLite, etc.
    /// </summary>
    public async Task LoadKeywordsFromDatabaseAsync(DatabaseType dialect = DatabaseType.MSSQL)
    {
        try
        {
            CurrentDialect = dialect;
            string dialectStr = dialect.ToDialectString();
            var dbKeywords = await DatabaseStorageService.Instance.GetKeywordsAsync(dialectStr);
            var dbSnippets = await DatabaseStorageService.Instance.GetSnippetsAsync(dialectStr);

            if (dbKeywords.Count > 0)
            {
                Keywords = dbKeywords;
                Snippets = dbSnippets;
                BuildRegex();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SqlKeywordsService] Failed to load from SQLite: {ex.Message}");
        }
    }

    private void BuildRegex()
    {
        // Sort by length descending to match composite keywords first (e.g. "CROSS APPLY" before "CROSS")
        var sorted = Keywords.OrderByDescending(k => k.Length).Select(Regex.Escape);
        string pattern = $@"\b({string.Join("|", sorted)})\b";
        KeywordRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
