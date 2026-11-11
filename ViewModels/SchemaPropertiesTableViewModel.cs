using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Models;

namespace Quell.ViewModels;

public partial class SchemaPropertiesTableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _tableName = "Users";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<TableColumnItem> Columns { get; } = new()
    {
        new TableColumnItem { Ordinal = 1, Name = "user_id", DataType = "INT", MaxLength = 4, IsNullable = false, IsPrimaryKey = true, Description = "Unique user identifier (Identity)" },
        new TableColumnItem { Ordinal = 2, Name = "username", DataType = "NVARCHAR", MaxLength = 50, IsNullable = false, Description = "Unique account username" },
        new TableColumnItem { Ordinal = 3, Name = "email", DataType = "NVARCHAR", MaxLength = 255, IsNullable = false, Description = "User primary email address" },
        new TableColumnItem { Ordinal = 4, Name = "password_hash", DataType = "VARCHAR", MaxLength = 256, IsNullable = false, Description = "Argon2 / PBKDF2 hash" },
        new TableColumnItem { Ordinal = 5, Name = "role_id", DataType = "INT", MaxLength = 4, IsNullable = false, IsForeignKey = true, DefaultValue = "1", Description = "FK -> Roles(id)" },
        new TableColumnItem { Ordinal = 6, Name = "is_active", DataType = "BIT", MaxLength = 1, IsNullable = false, DefaultValue = "1", Description = "Active account status flag" },
        new TableColumnItem { Ordinal = 7, Name = "created_at", DataType = "DATETIME2", MaxLength = 8, IsNullable = false, DefaultValue = "SYSUTCDATETIME()", Description = "Record creation timestamp" }
    };

    public void LoadDefaultSampleColumns(string tableName)
    {
        TableName = tableName;
        Columns.Clear();
        Columns.Add(new TableColumnItem { Ordinal = 1, Name = "id", DataType = "INT", MaxLength = 4, IsNullable = false, IsPrimaryKey = true, Description = "Primary Key (Auto-increment)" });
        Columns.Add(new TableColumnItem { Ordinal = 2, Name = "name", DataType = "VARCHAR", MaxLength = 100, IsNullable = false, Description = "Entity name" });
        Columns.Add(new TableColumnItem { Ordinal = 3, Name = "status", DataType = "VARCHAR", MaxLength = 50, IsNullable = true, DefaultValue = "'Active'", Description = "Status flag" });
        Columns.Add(new TableColumnItem { Ordinal = 4, Name = "created_at", DataType = "DATETIME", MaxLength = 8, IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", Description = "Creation timestamp" });
    }

    [RelayCommand]
    public async Task CopyColumnNamesAsync()
    {
        var names = string.Join(", ", Columns.Select(c => c.Name));
        await Quell.Services.ClipboardService.CopyTextAsync(names);
        StatusMessage = "Copied column names to clipboard!";
    }
}