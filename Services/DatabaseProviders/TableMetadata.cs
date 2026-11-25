namespace Quell.Services.DatabaseProviders;

public class TableMetadata
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public string TableType { get; set; } = "TABLE";
    public long? RowCount { get; set; }

    public string FullName => string.IsNullOrEmpty(SchemaName) || SchemaName == "main" 
        ? TableName 
        : $"{SchemaName}.{TableName}";

    public string DisplayType
    {
        get
        {
            string upper = (TableType ?? "TABLE").ToUpperInvariant();
            if (upper.Contains("VIEW")) return "VIEW";
            if (upper.Contains("PROC") || upper.Contains("FUNC")) return "PROC";
            return "TABLE";
        }
    }

    public bool IsTable => DisplayType == "TABLE";
    public bool IsView => DisplayType == "VIEW";
    public bool IsProcedure => DisplayType == "PROC";

    public string FormattedRowCount
    {
        get
        {
            if (!RowCount.HasValue || RowCount.Value < 0) return string.Empty;
            long count = RowCount.Value;
            if (count >= 1_000_000) return $"{count / 1_000_000.0:0.#}m";
            if (count >= 1_000) return $"{count / 1_000.0:0.#}k";
            return count.ToString();
        }
    }
}
