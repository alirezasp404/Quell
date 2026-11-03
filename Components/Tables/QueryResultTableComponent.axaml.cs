using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Fdb.Services;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class QueryResultTableComponent : UserControl
{
    private const string SelectedClass = "excel-selected";
    private const string ActiveClass = "excel-active";

    private static readonly PropertyInfo? OwningColumnProperty =
        typeof(DataGridCell).GetProperty(
            "OwningColumn",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private QueryResultTableViewModel? _viewModel;
    private QueryResultRow? _anchorRow;
    private int _anchorCol;
    private QueryResultRow? _activeRow;
    private int _activeCol;
    private QueryResultRow? _cornerARow;
    private QueryResultRow? _cornerBRow;
    private int _col1;
    private int _col2;
    private bool _hasSelection;
    private bool _dragging;
    private bool _dragRows;
    private bool _refreshQueued;
    private IPointer? _capturedPointer;

    public QueryResultTableComponent()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        ResultsDataGrid.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        ResultsDataGrid.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        ResultsDataGrid.AddHandler(
            InputElement.KeyDownEvent,
            OnGridKeyDown,
            RoutingStrategies.Tunnel);

        ThemeService.Instance.TableCustomizationChanged += OnTableCustomizationChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CopySelection = null;
            _viewModel.CopyFocusedRow = null;
            _viewModel.GetSelectedRowsCallback = null;
            _viewModel.GetSelectedColumnsCallback = null;
            _viewModel.ColumnsChanged -= OnColumnsChanged;
        }

        _viewModel = DataContext as QueryResultTableViewModel;
        if (_viewModel == null)
            return;

        _viewModel.CopySelection = BuildSelectionText;
        _viewModel.CopyFocusedRow = BuildFocusedRowText;
        _viewModel.GetSelectedRowsCallback = GetSelectedRowsList;
        _viewModel.GetSelectedColumnsCallback = GetSelectedColumnsList;
        _viewModel.ColumnsChanged -= OnColumnsChanged;
        _viewModel.ColumnsChanged += OnColumnsChanged;
    }

    private async void OnForeignKeyNavigationClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button btn && btn.DataContext is QueryResultRow row)
        {
            string colName = btn.Tag?.ToString() ?? "Role";
            string cellVal = row[colName];
            if (_viewModel != null)
            {
                await _viewModel.NavigateToForeignKeyAsync(colName, cellVal);
            }
        }
    }

    private void OnTableCustomizationChanged(object? sender, TableCustomizationRecord custom)
    {
        if (ResultsDataGrid != null)
        {
            if (custom.RowHeight >= 18)
            {
                ResultsDataGrid.RowHeight = custom.RowHeight;
            }
            if (ResultsDataGrid.Columns.Count > 0 && custom.GutterWidth >= 30)
            {
                ResultsDataGrid.Columns[0].Width = new DataGridLength(custom.GutterWidth);
            }
            ResultsDataGrid.GridLinesVisibility = custom.ShowGridLines 
                ? DataGridGridLinesVisibility.All 
                : DataGridGridLinesVisibility.None;
        }
    }

    private void OnColumnsChanged(IReadOnlyList<string> columns)
    {
        void UpdateColumns()
        {
            while (ResultsDataGrid.Columns.Count > 1)
            {
                ResultsDataGrid.Columns.RemoveAt(ResultsDataGrid.Columns.Count - 1);
            }

            for (int i = 0; i < columns.Count; i++)
            {
                var colName = columns[i];
                var isPk = _viewModel?.IsColumnPrimaryKey(colName) == true;
                var fkRel = _viewModel?.GetForeignKeyRelation(colName);
                var isFk = fkRel != null;
                var styleClass = isPk ? "pk-col" : (isFk ? "fk-col" : ((i % 2 == 0) ? "white-col" : "alt-col"));

                if (isFk && fkRel != null)
                {
                    var fkCol = new DataGridTemplateColumn
                    {
                        CanUserResize = true,
                        CanUserSort = true,
                        SortMemberPath = $"[{colName}]"
                    };

                    var headerPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 4,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    ToolTip.SetTip(headerPanel, $"Foreign Key -> References {fkRel.TargetTable}({fkRel.TargetColumn})");

                    var fkBadge = new Border
                    {
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F1F5F9")),
                        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CBD5E1")),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(3),
                        Padding = new Avalonia.Thickness(3, 1),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    var fkStack = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 2
                    };

                    var linkIcon = new Avalonia.Controls.PathIcon
                    {
                        Data = Avalonia.Media.Geometry.Parse("M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"),
                        Width = 9,
                        Height = 9,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#475569")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    var fkText = new TextBlock
                    {
                        Text = "FK",
                        FontSize = 9,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#475569")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    fkStack.Children.Add(linkIcon);
                    fkStack.Children.Add(fkText);
                    fkBadge.Child = fkStack;

                    var titleBlock = new TextBlock
                    {
                        Text = colName.ToUpperInvariant(),
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    headerPanel.Children.Add(fkBadge);
                    headerPanel.Children.Add(titleBlock);
                    fkCol.Header = headerPanel;

                    string capturedCol = colName;
                    var relCopy = fkRel;
                    fkCol.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<QueryResultRow>((row, ns) =>
                    {
                        var cellGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new Thickness(4, 0)
                        };

                        var cellText = new TextBlock
                        {
                            FontSize = 11,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        cellText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding($"[{capturedCol}]"));
                        Grid.SetColumn(cellText, 0);
                        cellGrid.Children.Add(cellText);

                        var arrowBtn = new Button
                        {
                            Classes = { "fk-nav-btn" },
                            Width = 20,
                            Height = 20,
                            Padding = new Thickness(0),
                            Background = Avalonia.Media.Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Cursor = new Cursor(StandardCursorType.Hand),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Tag = capturedCol
                        };
                        ToolTip.SetTip(arrowBtn, $"Navigate to referenced record in {relCopy.TargetTable} table");

                        var arrowIcon = new Avalonia.Controls.PathIcon
                        {
                            Data = Avalonia.Media.Geometry.Parse("M14 5l7 7m0 0l-7 7m7-7H3"),
                            Width = 10,
                            Height = 10,
                            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#64748B")),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        arrowBtn.Content = arrowIcon;

                        arrowBtn.Click += async (s, e) =>
                        {
                            e.Handled = true;
                            string val = row != null ? row[capturedCol] : string.Empty;
                            if (_viewModel != null)
                            {
                                await _viewModel.NavigateToForeignKeyAsync(capturedCol, val);
                            }
                        };

                        Grid.SetColumn(arrowBtn, 1);
                        cellGrid.Children.Add(arrowBtn);

                        return cellGrid;
                    });

                    fkCol.CellStyleClasses.Add(styleClass);
                    ResultsDataGrid.Columns.Add(fkCol);
                    continue;
                }

                var col = new DataGridTextColumn
                {
                    Binding = new Avalonia.Data.Binding($"[{colName}]"),
                    CanUserResize = true,
                    CanUserSort = true
                };

                if (isPk)
                {
                    var headerPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 4,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    var pkBadge = new Border
                    {
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F1F5F9")),
                        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CBD5E1")),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(3),
                        Padding = new Avalonia.Thickness(3, 1),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    var pkStack = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 2
                    };

                    var keyIcon = new Avalonia.Controls.PathIcon
                    {
                        Data = Avalonia.Media.Geometry.Parse("M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z"),
                        Width = 9,
                        Height = 9,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#64748B")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    var pkText = new TextBlock
                    {
                        Text = "PK",
                        FontSize = 9,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#64748B")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    pkStack.Children.Add(keyIcon);
                    pkStack.Children.Add(pkText);
                    pkBadge.Child = pkStack;

                    var titleBlock = new TextBlock
                    {
                        Text = colName.ToUpperInvariant(),
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A")),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };

                    headerPanel.Children.Add(pkBadge);
                    headerPanel.Children.Add(titleBlock);
                    col.Header = headerPanel;
                }
                else
                {
                    col.Header = colName.ToUpperInvariant();
                }

                col.CellStyleClasses.Add(styleClass);
                ResultsDataGrid.Columns.Add(col);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateColumns();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateColumns);
        }
    }

    private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.Row.DataContext is not QueryResultRow item || e.Column == null)
            return;

        var point = e.PointerPressedEventArgs.GetCurrentPoint(ResultsDataGrid);
        var extend = e.PointerPressedEventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var header = IsRowHeader(e.Column);

        if (point.Properties.IsRightButtonPressed)
        {
            var alreadySelected = header
                ? IsRowFullySelected(item)
                : IsCellSelected(item, e.Column.DisplayIndex);
            if (!alreadySelected)
                BeginPointerSelection(item, e.Column, extend: false);

            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (e.PointerPressedEventArgs.ClickCount == 2 && !header)
        {
            _viewModel?.OpenEditCell(item, e.Column.Header?.ToString());
            e.PointerPressedEventArgs.Handled = true;
            return;
        }

        BeginPointerSelection(item, e.Column, extend);
        _dragging = true;
        _capturedPointer = e.PointerPressedEventArgs.Pointer;
        _capturedPointer.Capture(ResultsDataGrid);
        ResultsDataGrid.Focus();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _anchorRow == null || HitTestCell(e) is not { } hit)
            return;

        if (_dragRows)
        {
            if (!ReferenceEquals(hit.Item, _activeRow))
                SelectRowSpan(_anchorRow, hit.Item, scroll: false);
            return;
        }

        if (IsRowHeader(hit.Column))
            return;

        if (ReferenceEquals(hit.Item, _activeRow) && hit.Column.DisplayIndex == _activeCol)
            return;

        ExtendTo(hit.Item, hit.Column.DisplayIndex, scroll: false);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        _dragRows = false;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        ApplyRow(e.Row);
        Dispatcher.UIThread.Post(() => ApplyRow(e.Row), DispatcherPriority.Loaded);
    }

    private void OnUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        foreach (var cell in e.Row.GetVisualDescendants().OfType<DataGridCell>())
        {
            cell.Classes.Remove(SelectedClass);
            cell.Classes.Remove(ActiveClass);
        }
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshVisuals, DispatcherPriority.Background);
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && e.Key == Key.C)
        {
            if (_viewModel?.CopyCellCommand.CanExecute(null) == true)
                _viewModel.CopyCellCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.A)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            var targetRow = _activeRow ?? _viewModel?.SelectedRow;
            var colHeader = (ResultsDataGrid.CurrentColumn ?? ResultsDataGrid.Columns.ElementAtOrDefault(_activeCol))?.Header?.ToString();
            _viewModel?.OpenEditCell(targetRow, colHeader);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (_viewModel?.DeleteSelectedRowCommand.CanExecute(null) == true)
            {
                _viewModel.DeleteSelectedRowCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F5)
        {
            if (_viewModel?.RefreshCommand.CanExecute(null) == true)
            {
                _viewModel.RefreshCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view || view.Count == 0)
            return;

        var data = DataColumnIndexes();
        if (data.Count == 0)
            return;

        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Tab or Key.Enter or Key.Home or Key.End or Key.PageUp or Key.PageDown))
            return;

        if (_activeRow == null)
        {
            if (view.GetItemAt(0) is QueryResultRow first)
                SelectCell(first, data[0], scroll: true);
            e.Handled = true;
            return;
        }

        var rowIndex = view.IndexOf(_activeRow);
        if (rowIndex < 0)
            rowIndex = 0;

        var colPos = data.IndexOf(_activeCol);
        if (colPos < 0)
            colPos = 0;

        switch (e.Key)
        {
            case Key.Left:
                colPos = ctrl ? 0 : Math.Max(0, colPos - 1);
                break;
            case Key.Right:
                colPos = ctrl ? data.Count - 1 : Math.Min(data.Count - 1, colPos + 1);
                break;
            case Key.Up:
                rowIndex = ctrl ? 0 : Math.Max(0, rowIndex - 1);
                break;
            case Key.Down:
                rowIndex = ctrl ? view.Count - 1 : Math.Min(view.Count - 1, rowIndex + 1);
                break;
            case Key.Home:
                colPos = 0;
                if (ctrl)
                    rowIndex = 0;
                break;
            case Key.End:
                colPos = data.Count - 1;
                if (ctrl)
                    rowIndex = view.Count - 1;
                break;
            case Key.PageUp:
                rowIndex = Math.Max(0, rowIndex - 10);
                break;
            case Key.PageDown:
                rowIndex = Math.Min(view.Count - 1, rowIndex + 10);
                break;
            case Key.Tab:
                colPos += shift ? -1 : 1;
                if (colPos >= data.Count)
                {
                    colPos = 0;
                    rowIndex++;
                }
                else if (colPos < 0)
                {
                    colPos = data.Count - 1;
                    rowIndex--;
                }

                if (rowIndex < 0 || rowIndex >= view.Count)
                    return;

                shift = false;
                break;
            case Key.Enter:
                rowIndex = shift
                    ? Math.Max(0, rowIndex - 1)
                    : Math.Min(view.Count - 1, rowIndex + 1);
                shift = false;
                break;
        }

        if (view.GetItemAt(rowIndex) is not QueryResultRow row)
            return;

        if (shift && _anchorRow != null)
            ExtendTo(row, data[colPos], scroll: true);
        else
            SelectCell(row, data[colPos], scroll: true);

        e.Handled = true;
    }

    private void BeginPointerSelection(QueryResultRow item, DataGridColumn column, bool extend)
    {
        if (IsRowHeader(column))
        {
            _dragRows = true;
            if (extend && _anchorRow != null)
                SelectRowSpan(_anchorRow, item, scroll: false);
            else
                SelectRowSpan(item, item, scroll: false);
            return;
        }

        _dragRows = false;
        if (extend && _anchorRow != null)
            ExtendTo(item, column.DisplayIndex, scroll: false);
        else
            SelectCell(item, column.DisplayIndex, scroll: false);
    }

    private void SelectCell(QueryResultRow row, int column, bool scroll)
    {
        ApplyState(row, row, column, column, row, column, row, column, scroll);
    }

    private void ExtendTo(QueryResultRow row, int column, bool scroll)
    {
        if (_anchorRow == null)
        {
            SelectCell(row, column, scroll);
            return;
        }

        ApplyState(_anchorRow, row, _anchorCol, column, row, column, _anchorRow, _anchorCol, scroll);
    }

    private void SelectRowSpan(QueryResultRow origin, QueryResultRow focus, bool scroll)
    {
        var data = DataColumnIndexes();
        if (data.Count == 0)
            return;

        ApplyState(origin, focus, data[0], data[^1], focus, data[0], origin, data[0], scroll);
    }

    private void SelectAll()
    {
        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view || view.Count == 0)
            return;

        var data = DataColumnIndexes();
        if (data.Count == 0)
            return;

        if (view.GetItemAt(0) is not QueryResultRow first || view.GetItemAt(view.Count - 1) is not QueryResultRow last)
            return;

        var activeRow = _activeRow ?? first;
        var activeCol = _activeRow == null ? data[0] : _activeCol;
        var anchorRow = _anchorRow ?? activeRow;
        var anchorCol = _anchorRow == null ? activeCol : _anchorCol;
        ApplyState(first, last, data[0], data[^1], activeRow, activeCol, anchorRow, anchorCol, scroll: true);
    }

    private void ApplyState(
        QueryResultRow cornerA,
        QueryResultRow cornerB,
        int columnA,
        int columnB,
        QueryResultRow activeRow,
        int activeColumn,
        QueryResultRow anchorRow,
        int anchorColumn,
        bool scroll)
    {
        _cornerARow = cornerA;
        _cornerBRow = cornerB;
        _col1 = Math.Min(columnA, columnB);
        _col2 = Math.Max(columnA, columnB);
        _activeRow = activeRow;
        _activeCol = activeColumn;
        _anchorRow = anchorRow;
        _anchorCol = anchorColumn;
        _hasSelection = true;
        SyncGridCurrency(scroll);
        QueueRefresh();
        UpdateStatus();
    }

    private void SyncGridCurrency(bool scroll)
    {
        if (_activeRow == null)
            return;

        ResultsDataGrid.SelectedItem = _activeRow;
        var column = ColumnAtDisplayIndex(_activeCol);
        if (column == null)
            return;

        ResultsDataGrid.CurrentColumn = column;
        if (scroll)
            ResultsDataGrid.ScrollIntoView(_activeRow, column);
    }

    private void QueueRefresh()
    {
        RefreshVisuals();
        if (_refreshQueued)
            return;

        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            RefreshVisuals();
        }, DispatcherPriority.Loaded);
    }

    private void RefreshVisuals()
    {
        foreach (var row in ResultsDataGrid.GetVisualDescendants().OfType<DataGridRow>())
            ApplyRow(row);
    }

    private void ApplyRow(DataGridRow row)
    {
        if (row.DataContext is not QueryResultRow item)
            return;

        foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>())
        {
            var column = ColumnOf(cell);
            if (column == null)
            {
                cell.Classes.Remove(SelectedClass);
                cell.Classes.Remove(ActiveClass);
                continue;
            }

            var header = IsRowHeader(column);
            var active = !header && IsActive(item, column.DisplayIndex);
            var selected = !active && (header
                ? IsRowFullySelected(item)
                : IsCellSelected(item, column.DisplayIndex));

            if (selected)
                cell.Classes.Add(SelectedClass);
            else
                cell.Classes.Remove(SelectedClass);

            if (active)
                cell.Classes.Add(ActiveClass);
            else
                cell.Classes.Remove(ActiveClass);
        }
    }

    private void UpdateStatus()
    {
        if (_viewModel == null)
            return;

        if (!TryGetSpan(out var r1, out var r2, out var c1, out var c2))
        {
            _viewModel.SelectionStatus = "Click a cell to select it · drag or Shift-click for a range";
            return;
        }

        var rows = r2 - r1 + 1;
        var cols = ResultsDataGrid.Columns.Count(column => column.DisplayIndex >= c1 && column.DisplayIndex <= c2);
        _viewModel.SelectionStatus = rows == 1 && cols == 1
            ? "1 cell selected"
            : $"{rows} × {cols} cells selected";
    }

    private string? BuildSelectionText()
    {
        if (!TryGetSpan(out var r1, out var r2, out var c1, out var c2))
            return null;
        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view)
            return null;

        var columns = ResultsDataGrid.Columns
            .Where(column => column.DisplayIndex >= c1 && column.DisplayIndex <= c2)
            .OrderBy(column => column.DisplayIndex)
            .ToList();

        var builder = new StringBuilder();
        for (var rowIndex = r1; rowIndex <= r2; rowIndex++)
        {
            if (rowIndex > r1)
                builder.Append("\r\n");
            if (view.GetItemAt(rowIndex) is not QueryResultRow row)
                continue;

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                if (columnIndex > 0)
                    builder.Append('\t');
                builder.Append(CellText(row, columns[columnIndex]));
            }
        }

        return builder.ToString();
    }

    private string? BuildFocusedRowText()
    {
        var row = _activeRow ?? ResultsDataGrid.SelectedItem as QueryResultRow;
        if (row == null)
            return null;

        var columns = ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column))
            .OrderBy(column => column.DisplayIndex)
            .Select(column => CellText(row, column));
        return string.Join('\t', columns);
    }

    private bool TryGetSpan(out int rowStart, out int rowEnd, out int columnStart, out int columnEnd)
    {
        rowStart = rowEnd = columnStart = columnEnd = 0;
        if (!_hasSelection || _cornerARow == null || _cornerBRow == null)
            return false;
        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view)
            return false;

        var first = view.IndexOf(_cornerARow);
        var second = view.IndexOf(_cornerBRow);
        if (first < 0 || second < 0)
            return false;

        rowStart = Math.Min(first, second);
        rowEnd = Math.Max(first, second);
        columnStart = _col1;
        columnEnd = _col2;
        return true;
    }

    private bool IsCellSelected(QueryResultRow item, int displayIndex)
    {
        if (!TryGetSpan(out var rowStart, out var rowEnd, out var columnStart, out var columnEnd))
            return false;
        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view)
            return false;

        var index = view.IndexOf(item);
        return index >= rowStart && index <= rowEnd && displayIndex >= columnStart && displayIndex <= columnEnd;
    }

    private bool IsActive(QueryResultRow item, int displayIndex) =>
        _hasSelection && ReferenceEquals(item, _activeRow) && displayIndex == _activeCol;

    private bool IsRowFullySelected(QueryResultRow item)
    {
        if (!TryGetSpan(out var rowStart, out var rowEnd, out var columnStart, out var columnEnd))
            return false;
        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view)
            return false;

        var index = view.IndexOf(item);
        if (index < rowStart || index > rowEnd)
            return false;

        var data = DataColumnIndexes();
        return data.Count > 0 && data[0] >= columnStart && data[^1] <= columnEnd;
    }

    private (QueryResultRow Item, DataGridColumn Column)? HitTestCell(PointerEventArgs e)
    {
        if (ResultsDataGrid.InputHitTest(e.GetPosition(ResultsDataGrid)) is not Visual visual)
            return null;

        var cell = visual as DataGridCell ?? visual.FindAncestorOfType<DataGridCell>();
        if (cell == null)
            return null;

        var row = DataGridRow.GetRowContainingElement(cell);
        if (row?.DataContext is not QueryResultRow item)
            return null;

        var column = ColumnOf(cell);
        return column == null ? null : (item, column);
    }

    private DataGridColumn? ColumnOf(DataGridCell cell)
    {
        if (OwningColumnProperty?.GetValue(cell) is DataGridColumn owning
            && ResultsDataGrid.Columns.Contains(owning))
        {
            return owning;
        }

        var row = DataGridRow.GetRowContainingElement(cell);
        if (row == null)
            return null;

        var cells = row.GetVisualDescendants().OfType<DataGridCell>().ToList();
        var index = cells.IndexOf(cell);
        var ordered = ResultsDataGrid.Columns.OrderBy(column => column.DisplayIndex).ToList();
        return index >= 0 && index < ordered.Count ? ordered[index] : null;
    }

    private DataGridColumn? ColumnAtDisplayIndex(int displayIndex) =>
        ResultsDataGrid.Columns.FirstOrDefault(column => column.DisplayIndex == displayIndex);

    private List<int> DataColumnIndexes() =>
        ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column))
            .Select(column => column.DisplayIndex)
            .OrderBy(index => index)
            .ToList();

    private static bool IsRowHeader(DataGridColumn column) =>
        column.Header?.ToString() == "ROW #";

    public IReadOnlyList<QueryResultRow> GetSelectedRowsList()
    {
        if (ResultsDataGrid.CollectionView is DataGridCollectionView view &&
            TryGetSpan(out var r1, out var r2, out _, out _))
        {
            var list = new List<QueryResultRow>();
            for (int i = r1; i <= r2 && i < view.Count; i++)
            {
                if (view.GetItemAt(i) is QueryResultRow row)
                {
                    list.Add(row);
                }
            }
            if (list.Count > 0)
                return list;
        }

        var active = _activeRow ?? ResultsDataGrid.SelectedItem as QueryResultRow;
        return active != null ? new[] { active } : Array.Empty<QueryResultRow>();
    }

    public IReadOnlyList<string> GetSelectedColumnsList()
    {
        if (!TryGetSpan(out _, out _, out var c1, out var c2))
        {
            var curCol = ResultsDataGrid.CurrentColumn;
            string? headerName = ExtractHeaderName(curCol);
            return !string.IsNullOrEmpty(headerName) && headerName != "ROW #" ? new[] { headerName } : Array.Empty<string>();
        }

        var columns = ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column) && column.DisplayIndex >= c1 && column.DisplayIndex <= c2)
            .OrderBy(column => column.DisplayIndex)
            .Select(ExtractHeaderName)
            .Where(h => !string.IsNullOrEmpty(h) && h != "ROW #")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return columns;
    }

    private static string ExtractHeaderName(DataGridColumn? column)
    {
        if (column == null) return string.Empty;
        if (column.Header is StackPanel sp)
        {
            var tb = sp.Children.OfType<TextBlock>().LastOrDefault();
            return tb?.Text ?? string.Empty;
        }
        return column.Header?.ToString() ?? string.Empty;
    }

    private static string CellText(QueryResultRow row, DataGridColumn column)
    {
        var header = column.Header?.ToString();
        if (header == "ROW #") return row.RowIndex.ToString();

        var value = header != null ? row[header] : string.Empty;

        if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }
}
