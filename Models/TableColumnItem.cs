namespace Quell.Models;

public class TableColumnItem
{
    public int Ordinal { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "VARCHAR";
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}
