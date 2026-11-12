using System;
using System.Collections.Generic;
using System.Linq;
using Quell.Models;
using Quell.Services.DatabaseProviders;
using Quell.ViewModels;

namespace Quell.Services.DemoData;

public sealed record DemoTableDataset(
    string TableName,
    List<string> Columns,
    List<QueryResultRow> Rows,
    List<string> PrimaryKeys,
    List<ForeignKeyRelation> ForeignKeys);

/// <summary>
/// Centralized provider for sample/demo data shown strictly when no live database connection is active.
/// Live database connections must never fall back to this class.
/// </summary>
public static class DemoDataProvider
{
    public static IReadOnlyList<string> GetSampleTableNames() =>
        new[] { "Users", "Orders", "Products", "Roles" };

    public static bool IsSampleTableName(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return false;
        return tableName.Equals("Orders", StringComparison.OrdinalIgnoreCase)
            || tableName.Equals("Users", StringComparison.OrdinalIgnoreCase)
            || tableName.Equals("Roles", StringComparison.OrdinalIgnoreCase)
            || tableName.Equals("Courses", StringComparison.OrdinalIgnoreCase)
            || tableName.Equals("Departments", StringComparison.OrdinalIgnoreCase);
    }

    public static DemoTableDataset GetDefaultUsersDataset()
    {
        var (dataset, _) = GetSampleTable("Users");
        return dataset;
    }

    public static (DemoTableDataset Dataset, QueryResultRow? MatchedRow) GetSampleTable(
        string tableName,
        string? filterColumn = null,
        string? filterValue = null)
    {
        if (tableName.Equals("Roles", StringComparison.OrdinalIgnoreCase))
        {
            var roleRows = new List<QueryResultRow>
            {
                new() { RowIndex = 1, Id = 1, Name = "DBA", Email = "dba@company.internal", Role = "SUPERUSER", Status = "Active", CreatedAt = "2025-01-01 00:00:00" },
                new() { RowIndex = 2, Id = 2, Name = "Developer", Email = "dev@company.internal", Role = "READ_WRITE", Status = "Active", CreatedAt = "2025-01-10 10:00:00" },
                new() { RowIndex = 3, Id = 3, Name = "Analyst", Email = "analyst@company.internal", Role = "READ_ONLY", Status = "Active", CreatedAt = "2025-01-15 12:30:00" },
                new() { RowIndex = 4, Id = 4, Name = "Admin", Email = "admin@company.internal", Role = "FULL_CONTROL", Status = "Active", CreatedAt = "2025-01-01 00:00:00" }
            };

            foreach (var r in roleRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["DESCRIPTION"] = r.Id switch
                {
                    1 => "Database Administrator with full cluster access",
                    2 => "Application Software Engineer",
                    3 => "Business & Data Intelligence Analyst",
                    4 => "System Administrator & Security Officer",
                    _ => "Standard Role"
                };
                r.Values["PERMISSIONS"] = r.Role;
                r.Values["CREATED AT"] = r.CreatedAt;
                SnapshotOriginalValues(r);
            }

            var match = string.IsNullOrWhiteSpace(filterValue)
                ? null
                : roleRows.FirstOrDefault(r =>
                    r.Id.ToString() == filterValue ||
                    (r.Values.TryGetValue("TITLE", out var t) && t.Equals(filterValue, StringComparison.OrdinalIgnoreCase)));

            return (new DemoTableDataset(
                "Roles",
                new List<string> { "ID", "TITLE", "DESCRIPTION", "PERMISSIONS", "CREATED AT" },
                roleRows,
                new List<string> { "ID" },
                new List<ForeignKeyRelation>()), match);
        }

        if (tableName.Equals("Departments", StringComparison.OrdinalIgnoreCase))
        {
            var deptRows = new List<QueryResultRow>
            {
                new() { RowIndex = 1, Id = 1, Name = "Engineering", Email = "eng@company.internal", Role = "Tech Park A", Status = "Active", CreatedAt = "$2,500,000" },
                new() { RowIndex = 2, Id = 2, Name = "Data Science", Email = "data@company.internal", Role = "Tech Park B", Status = "Active", CreatedAt = "$1,800,000" },
                new() { RowIndex = 3, Id = 3, Name = "IT Operations", Email = "ops@company.internal", Role = "Main Tower", Status = "Active", CreatedAt = "$1,200,000" }
            };

            foreach (var r in deptRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["NAME"] = r.Name;
                r.Values["BUILDING"] = r.Role;
                r.Values["BUDGET"] = r.CreatedAt;
                SnapshotOriginalValues(r);
            }

            var match = string.IsNullOrWhiteSpace(filterValue)
                ? null
                : deptRows.FirstOrDefault(r => r.Id.ToString() == filterValue || r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase));

            return (new DemoTableDataset(
                "Departments",
                new List<string> { "ID", "NAME", "BUILDING", "BUDGET" },
                deptRows,
                new List<string> { "ID" },
                new List<ForeignKeyRelation>()), match);
        }

        if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            var userRows = new List<QueryResultRow>
            {
                new() { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Course = "C# & Avalonia UI", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
                new() { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Course = "SQL Server Architecture", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
                new() { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Course = "Database Performance Tuning", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
                new() { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Course = "PostgreSQL Administration", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
                new() { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Course = "Cloud Databases & Docker", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
            };

            foreach (var r in userRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["NAME"] = r.Name;
                r.Values["EMAIL"] = r.Email;
                r.Values["ROLE"] = r.Role;
                r.Values["COURSE"] = r.Course;
                r.Values["STATUS"] = r.Status;
                r.Values["CREATED AT"] = r.CreatedAt;
                SnapshotOriginalValues(r);
            }

            var match = string.IsNullOrWhiteSpace(filterValue)
                ? null
                : userRows.FirstOrDefault(r =>
                    (!string.IsNullOrWhiteSpace(filterColumn) && r.Values.TryGetValue(filterColumn, out var colVal) && colVal.Equals(filterValue, StringComparison.OrdinalIgnoreCase)) ||
                    r.Id.ToString() == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Course.Equals(filterValue, StringComparison.OrdinalIgnoreCase));

            return (new DemoTableDataset(
                "Users",
                new List<string> { "ID", "NAME", "EMAIL", "ROLE", "COURSE", "STATUS", "CREATED AT" },
                userRows,
                new List<string> { "ID" },
                new List<ForeignKeyRelation>
                {
                    new() { ColumnName = "ROLE", TargetTable = "Roles", TargetColumn = "TITLE", ConstraintName = "FK_Users_Roles" },
                    new() { ColumnName = "COURSE", TargetTable = "Courses", TargetColumn = "TITLE", ConstraintName = "FK_Users_Courses" }
                }), match);
        }

        if (tableName.Equals("Courses", StringComparison.OrdinalIgnoreCase))
        {
            var courseRows = new List<QueryResultRow>
            {
                new() { RowIndex = 1, Id = 1, Name = "C# & Avalonia UI", Email = "Alice Johnson", Role = "101", Course = "Active", Status = "Active", CreatedAt = "40 Hours" },
                new() { RowIndex = 2, Id = 2, Name = "SQL Server Architecture", Email = "Bob Smith", Role = "102", Course = "Active", Status = "Active", CreatedAt = "50 Hours" },
                new() { RowIndex = 3, Id = 3, Name = "Database Performance Tuning", Email = "Charlie Davis", Role = "103", Course = "In Progress", Status = "In Progress", CreatedAt = "35 Hours" },
                new() { RowIndex = 4, Id = 4, Name = "PostgreSQL Administration", Email = "Dana White", Role = "104", Course = "Active", Status = "Active", CreatedAt = "45 Hours" },
                new() { RowIndex = 5, Id = 5, Name = "Cloud Databases & Docker", Email = "Evan Wright", Role = "105", Course = "Completed", Status = "Completed", CreatedAt = "30 Hours" }
            };

            foreach (var r in courseRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["STUDENT_NAME"] = r.Email;
                r.Values["DURATION"] = r.CreatedAt;
                r.Values["STATUS"] = r.Status;
                SnapshotOriginalValues(r);
            }

            var match = string.IsNullOrWhiteSpace(filterValue)
                ? null
                : courseRows.FirstOrDefault(r =>
                    r.Id.ToString() == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Email.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Role == filterValue);

            return (new DemoTableDataset(
                "Courses",
                new List<string> { "ID", "TITLE", "USER_ID", "STUDENT_NAME", "DURATION", "STATUS" },
                courseRows,
                new List<string> { "ID" },
                new List<ForeignKeyRelation>
                {
                    new() { ColumnName = "USER_ID", TargetTable = "Users", TargetColumn = "ID", ConstraintName = "FK_Courses_Users" },
                    new() { ColumnName = "STUDENT_NAME", TargetTable = "Users", TargetColumn = "NAME", ConstraintName = "FK_Courses_StudentName" },
                    new() { ColumnName = "TITLE", TargetTable = "Users", TargetColumn = "COURSE", ConstraintName = "FK_Courses_Users_Course" }
                }), match);
        }

        if (tableName.Equals("Orders", StringComparison.OrdinalIgnoreCase))
        {
            var orderRows = new List<QueryResultRow>
            {
                new() { RowIndex = 1, Id = 5001, Name = "ORD-2026-001", Email = "$249.50", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-01 10:20:00" },
                new() { RowIndex = 2, Id = 5002, Name = "ORD-2026-002", Email = "$1,120.00", Role = "102", Course = "Bob Smith", Status = "Processing", CreatedAt = "2026-04-02 14:15:30" },
                new() { RowIndex = 3, Id = 5003, Name = "ORD-2026-003", Email = "$75.00", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-03 09:00:12" },
                new() { RowIndex = 4, Id = 5004, Name = "ORD-2026-004", Email = "$430.80", Role = "103", Course = "Charlie Davis", Status = "Shipped", CreatedAt = "2026-04-04 16:45:00" },
                new() { RowIndex = 5, Id = 5005, Name = "ORD-2026-005", Email = "$89.99", Role = "104", Course = "Dana White", Status = "Delivered", CreatedAt = "2026-04-05 11:30:45" }
            };

            foreach (var r in orderRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["ORDER_NUMBER"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["CUSTOMER_NAME"] = r.Course;
                r.Values["TOTAL_AMOUNT"] = r.Email;
                r.Values["STATUS"] = r.Status;
                r.Values["ORDER_DATE"] = r.CreatedAt;
                SnapshotOriginalValues(r);
            }

            var match = string.IsNullOrWhiteSpace(filterValue)
                ? null
                : orderRows.FirstOrDefault(r =>
                    r.Id.ToString() == filterValue ||
                    r.Role == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    (r.Values.TryGetValue(filterColumn ?? string.Empty, out var cv) && cv.Equals(filterValue, StringComparison.OrdinalIgnoreCase)));

            return (new DemoTableDataset(
                "Orders",
                new List<string> { "ID", "ORDER_NUMBER", "USER_ID", "CUSTOMER_NAME", "TOTAL_AMOUNT", "STATUS", "ORDER_DATE" },
                orderRows,
                new List<string> { "ID" },
                new List<ForeignKeyRelation>
                {
                    new() { ColumnName = "USER_ID", TargetTable = "Users", TargetColumn = "ID", ConstraintName = "FK_Orders_Users" },
                    new() { ColumnName = "CUSTOMER_NAME", TargetTable = "Users", TargetColumn = "NAME", ConstraintName = "FK_Orders_Users_Name" }
                }), match);
        }

        var genRows = new List<QueryResultRow>
        {
            new() { RowIndex = 1, Id = 1, Name = filterValue ?? "Item 1", Email = $"{tableName.ToLower()}@test", Role = "Active", Status = "Standard", CreatedAt = DateTime.Now.ToString("yyyy-MM-dd") }
        };
        genRows[0].Values["ID"] = "1";
        genRows[0].Values["NAME"] = filterValue ?? "Item 1";
        genRows[0].Values["STATUS"] = "Active";
        SnapshotOriginalValues(genRows[0]);

        return (new DemoTableDataset(
            tableName,
            new List<string> { "ID", "NAME", "STATUS" },
            genRows,
            new List<string> { "ID" },
            new List<ForeignKeyRelation>()), genRows[0]);
    }

    public static QueryResultRow? GetSampleRow(string tableName, string targetColumn, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var (dataset, matched) = GetSampleTable(tableName, targetColumn, value);
        if (matched != null) return matched;

        return dataset.Rows.FirstOrDefault(r =>
            r.Values.TryGetValue(targetColumn, out var cv) &&
            cv.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    public static TableSchemaMetadata GetSampleTableMetadata(string tableName)
    {
        var (dataset, _) = GetSampleTable(tableName);
        var cols = dataset.Columns.Select(c =>
        {
            string dataType = c.ToUpperInvariant() switch
            {
                "ID" or "USER_ID" => "INT",
                "TOTAL_AMOUNT" or "SALARY" => "DECIMAL(10,2)",
                "CREATED AT" or "CREATED_AT" or "ORDER_DATE" => "DATETIME",
                _ => "VARCHAR(100)"
            };
            return (c, dataType);
        }).ToList();

        return new TableSchemaMetadata(cols, dataset.PrimaryKeys, dataset.ForeignKeys);
    }

    public static List<TableColumnItem> GetSampleSchemaColumns(string tableName)
    {
        if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            return new List<TableColumnItem>
            {
                new() { Ordinal = 1, Name = "Id", DataType = "INT", MaxLength = 4, IsNullable = false, IsPrimaryKey = true, Description = "Unique user identifier" },
                new() { Ordinal = 2, Name = "Name", DataType = "NVARCHAR", MaxLength = 100, IsNullable = false, Description = "User full name" },
                new() { Ordinal = 3, Name = "Email", DataType = "NVARCHAR", MaxLength = 255, IsNullable = false, Description = "User email address" },
                new() { Ordinal = 4, Name = "Role", DataType = "VARCHAR", MaxLength = 50, IsNullable = false, IsForeignKey = true, Description = "FK -> Roles(Title)" },
                new() { Ordinal = 5, Name = "Course", DataType = "VARCHAR", MaxLength = 100, IsNullable = true, IsForeignKey = true, Description = "FK -> Courses(Title)" },
                new() { Ordinal = 6, Name = "Status", DataType = "VARCHAR", MaxLength = 50, IsNullable = false, DefaultValue = "'Active'", Description = "Account status" },
                new() { Ordinal = 7, Name = "CreatedAt", DataType = "DATETIME", MaxLength = 8, IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", Description = "Record creation timestamp" }
            };
        }

        return new List<TableColumnItem>
        {
            new() { Ordinal = 1, Name = "id", DataType = "INT", MaxLength = 4, IsNullable = false, IsPrimaryKey = true, Description = "Primary Key" },
            new() { Ordinal = 2, Name = "name", DataType = "VARCHAR", MaxLength = 100, IsNullable = false, Description = "Entity name" },
            new() { Ordinal = 3, Name = "status", DataType = "VARCHAR", MaxLength = 50, IsNullable = true, DefaultValue = "'Active'", Description = "Status flag" },
            new() { Ordinal = 4, Name = "created_at", DataType = "DATETIME", MaxLength = 8, IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", Description = "Creation timestamp" }
        };
    }

    public static List<(string ColumnName, string DataType, bool IsPrimaryKey, bool IsRequired)> GetSampleImportColumns() =>
        new()
        {
            ("Id", "INT", true, true),
            ("FullName", "VARCHAR(100)", false, true),
            ("Email", "VARCHAR(150)", false, false),
            ("Salary", "DECIMAL(10,2)", false, false)
        };

    public static List<DiagramTableItem> GetSampleDiagramTables()
    {
        var list = new List<DiagramTableItem>();

        var users = new DiagramTableItem { TableName = "Users", Schema = "dbo", DatabaseType = DatabaseType.MSSQL };
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Name", DataType = "VARCHAR(100)" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Email", DataType = "VARCHAR(150)" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "RoleId", DataType = "INT", IsForeignKey = true, ReferencedTable = "Roles", ReferencedColumn = "Id" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "CreatedAt", DataType = "DATETIME" });
        users.OutgoingRelations.Add("Users.RoleId -> Roles.Id");
        list.Add(users);

        var roles = new DiagramTableItem { TableName = "Roles", Schema = "dbo", DatabaseType = DatabaseType.MSSQL };
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Title", DataType = "VARCHAR(50)" });
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Description", DataType = "VARCHAR(255)" });
        list.Add(roles);

        var orders = new DiagramTableItem { TableName = "Orders", Schema = "dbo", DatabaseType = DatabaseType.MSSQL };
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "UserId", DataType = "INT", IsForeignKey = true, ReferencedTable = "Users", ReferencedColumn = "Id" });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "TotalAmount", DataType = "DECIMAL(10,2)" });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "OrderDate", DataType = "DATETIME" });
        orders.OutgoingRelations.Add("Orders.UserId -> Users.Id");
        list.Add(orders);

        var products = new DiagramTableItem { TableName = "Products", Schema = "dbo", DatabaseType = DatabaseType.MSSQL };
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Name", DataType = "VARCHAR(100)" });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Price", DataType = "DECIMAL(10,2)" });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "StockQuantity", DataType = "INT" });
        list.Add(products);

        return list;
    }

    private static void SnapshotOriginalValues(QueryResultRow row)
    {
        foreach (var kvp in row.Values)
        {
            row.OriginalValues[kvp.Key] = kvp.Value;
        }
    }
}
