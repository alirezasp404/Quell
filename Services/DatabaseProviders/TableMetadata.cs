namespace Quell.Services.DatabaseProviders;

public class TableMetadata
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public string TableType { get; set; } = "TABLE";

    public string FullName => string.IsNullOrEmpty(SchemaName) || SchemaName == "main" 
        ? TableName 
        : $"{SchemaName}.{TableName}";

    public string DisplayType => TableType.ToUpperInvariant().Contains("VIEW") ? "VIEW" : "TABLE";
}
