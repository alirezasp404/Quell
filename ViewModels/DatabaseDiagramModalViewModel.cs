using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public class DiagramColumnItem
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ReferencedTable { get; set; }
    public string? ReferencedColumn { get; set; }

    public string BadgeText => IsPrimaryKey ? "PK" : (IsForeignKey ? "FK" : string.Empty);
    public string FullDisplay => $"{ColumnName} : {DataType}";
}

public partial class DiagramTableItem : ObservableObject
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string DatabaseType { get; set; } = "SQL";
    public string FullName => string.IsNullOrWhiteSpace(Schema) ? TableName : $"{Schema}.{TableName}";

    [ObservableProperty]
    private double _x = 50;

    [ObservableProperty]
    private double _y = 50;

    [ObservableProperty]
    private double _width = 260;

    [ObservableProperty]
    private double _height = 200;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHighlighted;

    public ObservableCollection<DiagramColumnItem> Columns { get; } = new();
    public ObservableCollection<string> OutgoingRelations { get; } = new();

    public int PrimaryKeysCount => Columns.Count(c => c.IsPrimaryKey);
    public int ForeignKeysCount => Columns.Count(c => c.IsForeignKey);
    public int TotalColumnsCount => Columns.Count;

    public DiagramTableItem Clone()
    {
        var item = new DiagramTableItem
        {
            TableName = TableName,
            Schema = Schema,
            DatabaseType = DatabaseType,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            IsSelected = IsSelected,
            IsHighlighted = IsHighlighted
        };

        foreach (var col in Columns)
        {
            item.Columns.Add(new DiagramColumnItem
            {
                ColumnName = col.ColumnName,
                DataType = col.DataType,
                IsPrimaryKey = col.IsPrimaryKey,
                IsForeignKey = col.IsForeignKey,
                ReferencedTable = col.ReferencedTable,
                ReferencedColumn = col.ReferencedColumn
            });
        }

        foreach (var rel in OutgoingRelations)
        {
            item.OutgoingRelations.Add(rel);
        }

        return item;
    }
}

public partial class DiagramRelationItem : ObservableObject
{
    public DiagramTableItem SourceTable { get; set; } = null!;
    public DiagramTableItem TargetTable { get; set; } = null!;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public string RelationText { get; set; } = string.Empty;

    [ObservableProperty]
    private Point _startPoint;

    [ObservableProperty]
    private Point _endPoint;

    [ObservableProperty]
    private bool _isHighlighted;

    public int StartDirection { get; set; } = 1; // +1 = right, -1 = left
    public int EndDirection { get; set; } = 1;   // +1 = right, -1 = left
    public double MidX { get; set; }
    public int RelationIndex { get; set; }

    public string ShortRelationText => string.IsNullOrWhiteSpace(SourceColumn)
        ? $"{SourceTable?.TableName} -> {TargetTable?.TableName}"
        : $"{SourceColumn} -> {TargetColumn}";
}

public partial class DatabaseDiagramModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _databaseKey = string.Empty;

    [ObservableProperty]
    private string _databaseTitle = "Active Database";

    [ObservableProperty]
    private string _databaseType = "SQL";

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _filterMode = "All"; // "All", "Relations", "PK"

    [ObservableProperty]
    private double _zoomLevel = 1.0; // 0.3 .. 2.5

    [ObservableProperty]
    private double _panX = 40.0;

    [ObservableProperty]
    private double _panY = 40.0;

    [ObservableProperty]
    private double _viewportWidth = 1000.0;

    [ObservableProperty]
    private double _viewportHeight = 550.0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isLoadedFromCache;

    [ObservableProperty]
    private DateTime? _cachedAt;

    [ObservableProperty]
    private string _cacheStatusBadge = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Analyzing database schema & relationships...";

    [ObservableProperty]
    private string _loadingDetailText = "Scanning tables, primary keys, and foreign keys...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private double _loadingProgress;

    public bool IsIndeterminate => LoadingProgress <= 0.0;

    public bool IsEmptyState => !IsLoading && FilteredTables.Count == 0;

    public ObservableCollection<DiagramTableItem> AllTables { get; } = new();
    public ObservableCollection<DiagramTableItem> FilteredTables { get; } = new();
    public ObservableCollection<DiagramRelationItem> Relations { get; } = new();

    public int TotalTablesCount => AllTables.Count;
    public int TotalRelationsCount => AllTables.Sum(t => t.OutgoingRelations.Count);
    public int TablesWithRelationsCount => AllTables.Count(t => t.OutgoingRelations.Count > 0 || t.Columns.Any(c => c.IsForeignKey));
    public int TablesWithPkCount => AllTables.Count(t => t.Columns.Any(c => c.IsPrimaryKey));

    public Func<string, Task>? OpenTableDataCallback { get; set; }
    public Action<string>? GenerateDdlCallback { get; set; }
    public Func<Task>? RefreshDiagramCallback { get; set; }
    public Func<bool, Task>? RefreshDiagramCallbackWithFlag { get; set; }

    public event Action? RequestCanvasUpdate;

    public DatabaseDiagramModalViewModel()
    {
    }

    [RelayCommand]
    public void Open()
    {
        IsOpen = true;
        ApplyFilter();
    }

    [RelayCommand]
    public void Close()
    {
        SyncPositionsToCache();
        IsOpen = false;
        IsLoading = false;
    }

    /// <summary>
    /// Synchronizes current table positions on the canvas back into the persistent in-memory cache.
    /// </summary>
    public void SyncPositionsToCache()
    {
        if (!string.IsNullOrWhiteSpace(DatabaseKey) && AllTables.Count > 0)
        {
            DiagramCacheService.Instance.UpdateTablePositions(DatabaseKey, AllTables);
        }
    }

    [RelayCommand]
    public void SetFilterMode(string mode)
    {
        FilterMode = mode;
        ApplyFilter();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        ApplyZoomAroundCenter(Math.Min(2.5, Math.Round(ZoomLevel + 0.15, 2)));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ApplyZoomAroundCenter(Math.Max(0.25, Math.Round(ZoomLevel - 0.15, 2)));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        ApplyZoomAroundCenter(1.0);
    }

    private void ApplyZoomAroundCenter(double newZoom)
    {
        if (Math.Abs(newZoom - ZoomLevel) < 0.001) return;

        double viewW = ViewportWidth > 100.0 ? ViewportWidth : 1000.0;
        double viewH = ViewportHeight > 100.0 ? ViewportHeight : 550.0;
        double cx = viewW / 2.0;
        double cy = viewH / 2.0;

        double canvasCx = (cx - PanX) / ZoomLevel;
        double canvasCy = (cy - PanY) / ZoomLevel;

        ZoomLevel = newZoom;
        PanX = Math.Round(cx - canvasCx * newZoom, 1);
        PanY = Math.Round(cy - canvasCy * newZoom, 1);

        RequestCanvasUpdate?.Invoke();
    }

    [RelayCommand]
    public void ResetView()
    {
        ZoomLevel = 1.0;
        PanX = 40.0;
        PanY = 40.0;
        RequestCanvasUpdate?.Invoke();
    }

    [RelayCommand]
    public void FitToView()
    {
        if (FilteredTables.Count == 0)
        {
            ResetView();
            return;
        }

        double minX = FilteredTables.Min(t => t.X);
        double minY = FilteredTables.Min(t => t.Y);
        double maxX = FilteredTables.Max(t => t.X + (t.Width > 0 ? t.Width : 260.0));
        double maxY = FilteredTables.Max(t => t.Y + (t.Height > 0 ? t.Height : 200.0));

        double totalW = Math.Max(100.0, maxX - minX);
        double totalH = Math.Max(100.0, maxY - minY);

        double viewW = ViewportWidth > 100.0 ? ViewportWidth : 1000.0;
        double viewH = ViewportHeight > 100.0 ? ViewportHeight : 550.0;

        // Symmetric padding around content
        double padX = 60.0;
        double padY = 50.0;

        double availW = Math.Max(100.0, viewW - padX * 2.0);
        double availH = Math.Max(100.0, viewH - padY * 2.0);

        double scaleX = availW / totalW;
        double scaleY = availH / totalH;
        double bestZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.25, 1.25);

        ZoomLevel = Math.Round(bestZoom, 2);

        // Center content perfectly within viewW and viewH
        double contentScreenWidth = totalW * ZoomLevel;
        double contentScreenHeight = totalH * ZoomLevel;

        PanX = Math.Round((viewW - contentScreenWidth) / 2.0 - minX * ZoomLevel, 1);
        PanY = Math.Round((viewH - contentScreenHeight) / 2.0 - minY * ZoomLevel, 1);

        RequestCanvasUpdate?.Invoke();
    }

    [RelayCommand]
    public void AutoLayout()
    {
        if (FilteredTables.Count == 0) return;

        int count = FilteredTables.Count;
        var tableList = FilteredTables.ToList();

        // 1. Calculate dependency layer for each table
        // Parent tables (referenced by others, 0 outgoing foreign keys) -> Layer 0
        // Dependent tables -> 1 + Max(parent layers)
        var layers = new Dictionary<DiagramTableItem, int>();
        var tableByName = tableList.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

        List<DiagramTableItem> GetParents(DiagramTableItem t)
        {
            var parents = new List<DiagramTableItem>();
            foreach (var col in t.Columns)
            {
                if (col.IsForeignKey && !string.IsNullOrWhiteSpace(col.ReferencedTable) &&
                    tableByName.TryGetValue(col.ReferencedTable, out var parent) && parent != t)
                {
                    if (!parents.Contains(parent)) parents.Add(parent);
                }
            }
            foreach (var relStr in t.OutgoingRelations)
            {
                var parts = relStr.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var tgtName = parts[1].Trim().Split('.')[0].Trim();
                    if (tableByName.TryGetValue(tgtName, out var parent) && parent != t)
                    {
                        if (!parents.Contains(parent)) parents.Add(parent);
                    }
                }
            }
            return parents;
        }

        int ComputeLayer(DiagramTableItem t, HashSet<DiagramTableItem> visited)
        {
            if (layers.TryGetValue(t, out int cached)) return cached;
            if (visited.Contains(t)) return 0; // Break circular dependency cycle

            visited.Add(t);
            var parents = GetParents(t);
            int layer = 0;
            if (parents.Count > 0)
            {
                int maxParent = 0;
                foreach (var p in parents)
                {
                    maxParent = Math.Max(maxParent, ComputeLayer(p, visited));
                }
                layer = maxParent + 1;
            }
            visited.Remove(t);
            layers[t] = layer;
            return layer;
        }

        foreach (var t in tableList)
        {
            ComputeLayer(t, new HashSet<DiagramTableItem>());
        }

        // 2. Group tables by dependency layer
        var layerGroups = tableList
            .GroupBy(t => layers.GetValueOrDefault(t, 0))
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(t => t.TableName).ToList())
            .ToList();

        // 3. Balance columns so no column becomes excessively tall
        var finalColumns = new List<List<DiagramTableItem>>();
        int maxPerCol = Math.Max(3, (int)Math.Ceiling((double)count / 4.0));

        foreach (var group in layerGroups)
        {
            if (group.Count <= maxPerCol)
            {
                finalColumns.Add(group);
            }
            else
            {
                for (int i = 0; i < group.Count; i += maxPerCol)
                {
                    finalColumns.Add(group.Skip(i).Take(maxPerCol).ToList());
                }
            }
        }

        // If there are too many columns (> 5), merge sparse adjacent columns
        while (finalColumns.Count > 5)
        {
            int minSum = int.MaxValue;
            int mergeIdx = 0;
            for (int i = 0; i < finalColumns.Count - 1; i++)
            {
                int sum = finalColumns[i].Count + finalColumns[i + 1].Count;
                if (sum < minSum)
                {
                    minSum = sum;
                    mergeIdx = i;
                }
            }
            finalColumns[mergeIdx].AddRange(finalColumns[mergeIdx + 1]);
            finalColumns.RemoveAt(mergeIdx + 1);
        }

        // 4. Arrange tables by column with generous margins and padding
        double startX = 60.0;
        double startY = 60.0;
        double colWidth = 260.0;
        double xSpacing = 160.0; // Ample gutter for connection lines & badges
        double ySpacing = 36.0;

        for (int c = 0; c < finalColumns.Count; c++)
        {
            double curY = startY;
            double curX = startX + c * (colWidth + xSpacing);

            foreach (var table in finalColumns[c])
            {
                double estimatedHeight = 38.0 + 8.0 + Math.Min(table.Columns.Count, 11) * 20.0 + (table.OutgoingRelations.Count > 0 ? 28.0 : 0.0);

                table.X = curX;
                table.Y = curY;
                table.Width = colWidth;
                table.Height = estimatedHeight;

                curY += estimatedHeight + ySpacing;
            }
        }

        RecalculateRelationPoints();
        SyncPositionsToCache();
        FitToView();
    }

    [RelayCommand]
    public async Task RefreshDiagramAsync()
    {
        // Force refresh from database and update cache
        if (RefreshDiagramCallbackWithFlag != null)
        {
            await RefreshDiagramCallbackWithFlag(true);
        }
        else if (RefreshDiagramCallback != null)
        {
            await RefreshDiagramCallback();
        }
    }

    [RelayCommand]
    public async Task ClearCacheAndReloadAsync()
    {
        if (!string.IsNullOrWhiteSpace(DatabaseKey))
        {
            DiagramCacheService.Instance.Invalidate(DatabaseKey);
        }

        if (RefreshDiagramCallbackWithFlag != null)
        {
            await RefreshDiagramCallbackWithFlag(true);
        }
        else if (RefreshDiagramCallback != null)
        {
            await RefreshDiagramCallback();
        }
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnFilterModeChanged(string value)
    {
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredTables.Clear();
        var filter = SearchFilter?.Trim();

        foreach (var table in AllTables)
        {
            if (FilterMode == "Relations" && table.OutgoingRelations.Count == 0 && !table.Columns.Any(c => c.IsForeignKey))
            {
                continue;
            }
            if (FilterMode == "PK" && !table.Columns.Any(c => c.IsPrimaryKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(filter) ||
                table.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                table.Columns.Any(c => c.ColumnName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTables.Add(table);
            }
        }

        RebuildRelations();

        OnPropertyChanged(nameof(TotalTablesCount));
        OnPropertyChanged(nameof(TotalRelationsCount));
        OnPropertyChanged(nameof(TablesWithRelationsCount));
        OnPropertyChanged(nameof(TablesWithPkCount));
        OnPropertyChanged(nameof(IsEmptyState));
    }

    public void RebuildRelations()
    {
        Relations.Clear();

        foreach (var table in FilteredTables)
        {
            // 1. Foreign Keys detected on columns
            foreach (var col in table.Columns)
            {
                if (col.IsForeignKey && !string.IsNullOrWhiteSpace(col.ReferencedTable))
                {
                    var target = FilteredTables.FirstOrDefault(t => t.TableName.Equals(col.ReferencedTable, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                    {
                        var rel = new DiagramRelationItem
                        {
                            SourceTable = table,
                            TargetTable = target,
                            SourceColumn = col.ColumnName,
                            TargetColumn = col.ReferencedColumn ?? "Id",
                            RelationText = $"{table.TableName}.{col.ColumnName} → {target.TableName}.{col.ReferencedColumn ?? "Id"}"
                        };
                        Relations.Add(rel);
                    }
                }
            }

            // 2. OutgoingRelations string entries
            foreach (var relStr in table.OutgoingRelations)
            {
                var parts = relStr.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var srcParts = parts[0].Trim().Split('.');
                    var tgtParts = parts[1].Trim().Split('.');
                    string targetTableName = tgtParts[0].Trim();
                    string targetColName = tgtParts.Length > 1 ? tgtParts[1].Trim() : "Id";
                    string srcColName = srcParts.Length > 1 ? srcParts[1].Trim() : "";

                    if (!Relations.Any(r => r.SourceTable == table && r.TargetTable.TableName.Equals(targetTableName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var target = FilteredTables.FirstOrDefault(t => t.TableName.Equals(targetTableName, StringComparison.OrdinalIgnoreCase));
                        if (target != null)
                        {
                            Relations.Add(new DiagramRelationItem
                            {
                                SourceTable = table,
                                TargetTable = target,
                                SourceColumn = srcColName,
                                TargetColumn = targetColName,
                                RelationText = $"{table.TableName}.{srcColName} → {target.TableName}.{targetColName}"
                            });
                        }
                    }
                }
            }
        }

        RecalculateRelationPoints();
    }

    public void RecalculateRelationPoints()
    {
        for (int i = 0; i < Relations.Count; i++)
        {
            var rel = Relations[i];
            var src = rel.SourceTable;
            var tgt = rel.TargetTable;
            rel.RelationIndex = i;

            // 1. Accurately compute anchor Y on specific column row
            // Table Card Header = ~38px, top padding/margin = ~6px.
            // First column center is at Y + 54. Each subsequent column row is ~20px.
            // Clamped to 10 to stay within card ScrollViewer viewport.
            int srcColIdx = src.Columns.ToList().FindIndex(c => c.ColumnName.Equals(rel.SourceColumn, StringComparison.OrdinalIgnoreCase));
            double srcY = srcColIdx >= 0 
                ? (src.Y + 54.0 + Math.Min(srcColIdx, 10) * 20.0) 
                : (src.Y + (src.Height > 0 ? src.Height / 2.0 : 80.0));

            int tgtColIdx = tgt.Columns.ToList().FindIndex(c => c.ColumnName.Equals(rel.TargetColumn, StringComparison.OrdinalIgnoreCase));
            double tgtY = tgtColIdx >= 0 
                ? (tgt.Y + 54.0 + Math.Min(tgtColIdx, 10) * 20.0) 
                : (tgt.Y + (tgt.Height > 0 ? tgt.Height / 2.0 : 80.0));

            double srcRight = src.X + src.Width;
            double tgtRight = tgt.X + tgt.Width;

            double srcX, tgtX;
            int startDir, endDir;
            double midX;

            // Determine routing topology:
            if (srcRight + 24.0 <= tgt.X)
            {
                // Source is completely left of Target
                // Exit right (+1), enter target's left edge moving right (+1)
                srcX = srcRight;
                tgtX = tgt.X;
                startDir = 1;
                endDir = 1;
                midX = (srcX + tgtX) / 2.0;
            }
            else if (tgtRight + 24.0 <= src.X)
            {
                // Target is completely left of Source
                // Exit left (-1), enter target's right edge moving left (-1)
                srcX = src.X;
                tgtX = tgtRight;
                startDir = -1;
                endDir = -1;
                midX = (srcX + tgtX) / 2.0;
            }
            else
            {
                // Horizontally overlapping (same column, or partially overlapping cards)
                // Route along the right clearance gutter so the line loops cleanly around the outside
                srcX = srcRight;
                tgtX = tgtRight;
                startDir = 1;  // exit right into gutter
                endDir = -1;  // enter target right edge from gutter heading left
                double maxRight = Math.Max(srcRight, tgtRight);
                midX = maxRight + 36.0 + (i % 4) * 14.0;
            }

            rel.StartPoint = new Point(srcX, srcY);
            rel.EndPoint = new Point(tgtX, tgtY);
            rel.StartDirection = startDir;
            rel.EndDirection = endDir;
            rel.MidX = midX;
        }

        RequestCanvasUpdate?.Invoke();
    }

    /// <summary>
    /// Loads diagram tables from in-memory cache, preserving saved coordinates.
    /// </summary>
    public void LoadFromCache(IEnumerable<DiagramTableItem> tables, string databaseName, string dbType, string dbKey, DateTime? cachedAt)
    {
        DatabaseTitle = databaseName;
        DatabaseType = dbType;
        DatabaseKey = dbKey;
        IsLoadedFromCache = true;
        CachedAt = cachedAt;
        CacheStatusBadge = "Cached";

        AllTables.Clear();
        foreach (var t in tables)
        {
            AllTables.Add(t);
        }

        ApplyFilter();

        // If tables already have distinct coordinates, recalculate lines; otherwise layout
        bool hasCoordinates = AllTables.Any(t => t.X != 50 || t.Y != 50);
        if (hasCoordinates)
        {
            RecalculateRelationPoints();
        }
        else
        {
            AutoLayout();
        }

        IsLoading = false;
    }

    /// <summary>
    /// Loads freshly analyzed diagram tables from live database.
    /// </summary>
    public void LoadTables(IEnumerable<DiagramTableItem> tables, string databaseName, string dbType, string dbKey = "")
    {
        DatabaseTitle = databaseName;
        DatabaseType = dbType;
        if (!string.IsNullOrEmpty(dbKey)) DatabaseKey = dbKey;
        IsLoadedFromCache = false;
        CachedAt = DateTime.Now;
        CacheStatusBadge = "Live";

        AllTables.Clear();
        foreach (var t in tables)
        {
            AllTables.Add(t);
        }

        ApplyFilter();
        AutoLayout();
        IsLoading = false;
    }

    [RelayCommand]
    public async Task CopyMermaidDiagramAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        foreach (var table in AllTables)
        {
            sb.AppendLine($"    {table.TableName} {{");
            foreach (var col in table.Columns)
            {
                string keyType = col.IsPrimaryKey ? "PK" : (col.IsForeignKey ? "FK" : "");
                string cleanType = col.DataType.Replace(" ", "_").Replace("(", "_").Replace(")", "_");
                if (string.IsNullOrEmpty(cleanType)) cleanType = "string";
                sb.AppendLine($"        {cleanType} {col.ColumnName} {keyType}".TrimEnd());
            }
            sb.AppendLine("    }");

            foreach (var rel in table.OutgoingRelations)
            {
                var parts = rel.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string target = parts[1].Trim().Split(' ')[0];
                    sb.AppendLine($"    {table.TableName} }}o--|| {target} : references");
                }
            }
        }

        string mermaid = sb.ToString();

        try
        {
            await ClipboardService.CopyTextAsync(mermaid);
            StatusMessage = "Mermaid ER Diagram copied to clipboard!";
            HasStatusMessage = true;
        }
        catch
        {
            StatusMessage = "Mermaid ER Diagram generated!";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    public async Task ViewTableData(DiagramTableItem? table)
    {
        if (table == null || OpenTableDataCallback == null) return;
        SyncPositionsToCache();
        IsOpen = false;
        await OpenTableDataCallback.Invoke(table.FullName);
    }

    [RelayCommand]
    public void GenerateDdl(DiagramTableItem? table)
    {
        if (table == null || GenerateDdlCallback == null) return;
        SyncPositionsToCache();
        IsOpen = false;
        GenerateDdlCallback.Invoke(table.FullName);
    }
}
