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
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Quell.Services;
using Quell.Services.DatabaseProviders;
using Quell.ViewModels;

namespace Quell.Components;

public partial class QueryResultTableComponent : UserControl
{
    internal sealed record GridContextTarget(
        QueryResultRow? Row,
        string? ColumnName,
        bool IsRowHeaderOnly,
        bool CanCopy,
        Control? PlacementControl);

    private const string SelectedClass = "excel-selected";
    private const string ActiveClass = "excel-active";
    private const string GutterActiveClass = "excel-gutter-active";
    private const string HeaderActiveClass = "excel-header-active";
    private const string RowEvenClass = "excel-row-even";
    private const string RowOddClass = "excel-row-odd";

    private static readonly PropertyInfo? OwningColumnProperty =
        typeof(DataGridCell).GetProperty(
            "OwningColumn",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly Func<DataGridCell, DataGridColumn?>? OwningColumnGetter =
        CreateOwningColumnGetter();

    private static Func<DataGridCell, DataGridColumn?>? CreateOwningColumnGetter()
    {
        try
        {
            var prop = typeof(DataGridCell).GetProperty(
                "OwningColumn",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.GetMethod != null)
            {
                return (Func<DataGridCell, DataGridColumn?>)Delegate.CreateDelegate(
                    typeof(Func<DataGridCell, DataGridColumn?>), prop.GetMethod);
            }
        }
        catch { }
        return null;
    }

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
    private bool _selectedViaRowHeader;
    private bool _dragging;
    private bool _dragRows;
    private bool _refreshQueued;
    private IPointer? _capturedPointer;
    private IDisposable? _contextMenuSubscription;
    private GridContextTarget? _pendingRightClickTarget;

    private int _spanRowStart = -1;
    private int _spanRowEnd = -1;
    private bool _spanValid;
    private int _dataMinCol = 1;
    private int _dataMaxCol = 1;
    private List<int> _cachedDataColumnIndexes = new();

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

        _contextMenuSubscription = AttachGridContextMenu();
        Loaded += (_, _) =>
        {
            _contextMenuSubscription ??= AttachGridContextMenu();
            ApplyTableCustomizationToGrid(ThemeService.Instance.CurrentTableCustomization);
        };
        Unloaded += (_, _) =>
        {
            _contextMenuSubscription?.Dispose();
            _contextMenuSubscription = null;
        };

        ThemeService.Instance.TableCustomizationChanged += OnTableCustomizationChanged;
    }

    private void OnSelectAllCornerClick(object? sender, RoutedEventArgs e)
    {
        SelectAll();
        ResultsDataGrid?.Focus();
    }

    // Handles keyboard Shift+F10 / Menu key via ContextRequestedEvent; mouse right-clicks
    // are committed on CellPointerPressed and opened on PointerReleased (handledEventsToo:true)
    // so DataGrid's internal cell handling never suppresses them.
    private IDisposable AttachGridContextMenu() =>
        ContextMenuService.Attach(
            ResultsDataGrid,
            request => request.IsPointerTriggered ? null : ResolveGridContext(request),
            ConfigureGridContextMenu,
            ctx => ctx.PlacementControl ?? ResultsDataGrid,
            RoutingStrategies.Bubble);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CopySelection = null;
            _viewModel.CopyFocusedRow = null;
            _viewModel.GetSelectedRowsCallback = null;
            _viewModel.GetSelectedColumnsCallback = null;
            _viewModel.GetClipboardTextCallback = null;
            _viewModel.ResolvePasteTargetsCallback = null;
            _viewModel.CopyActiveCellCallback = null;
            _viewModel.EditActiveCellCallback = null;
            _viewModel.ColumnsChanged -= OnColumnsChanged;
        }

        _viewModel = DataContext as QueryResultTableViewModel;
        if (_viewModel == null)
            return;

        _viewModel.CopySelection = BuildSelectionText;
        _viewModel.CopyFocusedRow = BuildFocusedRowText;
        _viewModel.GetSelectedRowsCallback = GetSelectedRowsList;
        _viewModel.GetSelectedColumnsCallback = GetSelectedColumnsList;
        _viewModel.GetClipboardTextCallback = () => ClipboardService.GetTextAsync(ResultsDataGrid);
        _viewModel.ResolvePasteTargetsCallback = ResolvePasteTargets;
        _viewModel.CopyActiveCellCallback = () =>
        {
            if (_viewModel.CopyCellCommand.CanExecute(null))
                _viewModel.CopyCellCommand.Execute(null);
        };
        _viewModel.EditActiveCellCallback = () =>
        {
            if (_selectedViaRowHeader || _viewModel == null || !_viewModel.HasRows) return;
            var targetRow = _activeRow ?? _viewModel.SelectedRow;
            var colHeader = ExtractHeaderName(ResultsDataGrid.CurrentColumn ?? ResultsDataGrid.Columns.ElementAtOrDefault(_activeCol));
            if (targetRow != null && !string.IsNullOrWhiteSpace(colHeader) && colHeader != "ROW #")
            {
                _viewModel.OpenEditCell(targetRow, colHeader);
            }
        };
        _viewModel.ColumnsChanged -= OnColumnsChanged;
        _viewModel.ColumnsChanged += OnColumnsChanged;

        ApplyTableCustomizationToGrid(ThemeService.Instance.CurrentTableCustomization);

        if (_viewModel.LastColumns != null && _viewModel.LastColumns.Count > 0)
        {
            OnColumnsChanged(_viewModel.LastColumns);
        }
    }

    private void OnTableCustomizationChanged(object? sender, TableCustomizationRecord custom)
    {
        ApplyTableCustomizationToGrid(custom);
    }

    private void ApplyTableCustomizationToGrid(TableCustomizationRecord custom)
    {
        if (ResultsDataGrid == null) return;

        if (custom.RowHeight >= 18)
        {
            ResultsDataGrid.RowHeight = custom.RowHeight;
        }
        if (ResultsDataGrid.Columns.Count > 0 && custom.GutterWidth >= 30)
        {
            ResultsDataGrid.Columns[0].Width = new DataGridLength(custom.GutterWidth);
        }

        if (!custom.ShowGridLines)
        {
            ResultsDataGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        }
        else
        {
            ResultsDataGrid.GridLinesVisibility = (custom.GridLinesMode ?? "All").ToUpperInvariant() switch
            {
                "NONE" => DataGridGridLinesVisibility.None,
                "HORIZONTAL" => DataGridGridLinesVisibility.Horizontal,
                "VERTICAL" => DataGridGridLinesVisibility.Vertical,
                _ => DataGridGridLinesVisibility.All
            };
        }
    }

    public void ClearSelection()
    {
        _anchorRow = null;
        _activeRow = null;
        _cornerARow = null;
        _cornerBRow = null;
        _hasSelection = false;
        _selectedViaRowHeader = false;
        _spanValid = false;
        _spanRowStart = -1;
        _spanRowEnd = -1;
        _dragging = false;
        _dragRows = false;
        _pendingRightClickTarget = null;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;

        if (ResultsDataGrid != null)
        {
            ResultsDataGrid.SelectedItem = null;
        }
        UpdateStatus();
    }

    private void OnColumnsChanged(IReadOnlyList<string> columns)
    {
        void UpdateColumns()
        {
            try
            {
                ClearSelection();

                while (ResultsDataGrid.Columns.Count > 1)
                {
                    ResultsDataGrid.Columns.RemoveAt(ResultsDataGrid.Columns.Count - 1);
                }

                string stripingMode = (_viewModel?.StripingMode ?? "Rows").ToUpperInvariant();
                bool showDataTypes = _viewModel?.ShowColumnDataTypes == true;

                for (int i = 0; i < columns.Count; i++)
                {
                    var colName = columns[i];
                    var isPk = _viewModel?.IsColumnPrimaryKey(colName) == true;
                    var fkRel = _viewModel?.GetForeignKeyRelation(colName);
                    var isFk = fkRel != null;

                    string stripeClass = stripingMode switch
                    {
                        "ROWS" => "stripe-row",
                        "NONE" => "flat-col",
                        _ => (i % 2 == 0) ? "white-col" : "alt-col"
                    };

                    if (isFk && fkRel != null)
                    {
                        var fkCol = new DataGridTemplateColumn
                        {
                            Tag = colName,
                            CanUserResize = true,
                            CanUserSort = true,
                            SortMemberPath = $"[{colName}]",
                            Header = BuildExcelColumnHeader(colName, isPk, fkRel, showDataTypes)
                        };

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

                            var linkPill = new Border
                            {
                                Background = Avalonia.Media.Brushes.Transparent,
                                BorderThickness = new Thickness(0),
                                Padding = new Thickness(2, 0),
                                Cursor = new Cursor(StandardCursorType.Hand),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                                Tag = capturedCol
                            };

                            var valueStack = new StackPanel
                            {
                                Orientation = Avalonia.Layout.Orientation.Horizontal,
                                Spacing = 4,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };

                            var linkIcon = new Avalonia.Controls.PathIcon
                            {
                                Data = Avalonia.Media.Geometry.Parse("M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"),
                                Width = 8,
                                Height = 8,
                                Opacity = 0.8,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };
                            linkIcon[!Avalonia.Controls.PathIcon.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppAccentBrush");

                            var cellText = new TextBlock
                            {
                                FontSize = 11,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };
                            cellText[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppAccentBrush");

                            void UpdateFkText(object? dc)
                            {
                                if (dc is QueryResultRow r)
                                {
                                    cellText.Text = r[capturedCol];
                                }
                                else
                                {
                                    cellText.Text = string.Empty;
                                }
                            }
                            UpdateFkText(row);
                            cellGrid.DataContextChanged += (s, e) => UpdateFkText(cellGrid.DataContext);

                            valueStack.Children.Add(linkIcon);
                            valueStack.Children.Add(cellText);
                            linkPill.Child = valueStack;

                            ToolTip.SetTip(linkPill, $"Foreign Key: {relCopy.TargetTable}({relCopy.TargetColumn})\nDouble-click to navigate directly to referenced record");

                            // Double-click on cell or value directly navigates to the referenced table WITHOUT opening a modal
                            linkPill.DoubleTapped += async (s, e) =>
                            {
                                e.Handled = true;
                                string val = row != null ? row[capturedCol] : string.Empty;
                                if (string.IsNullOrWhiteSpace(val) || val.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return;
                                if (_viewModel != null)
                                {
                                    await _viewModel.NavigateToForeignKeyAsync(capturedCol, val);
                                }
                            };

                            cellGrid.DoubleTapped += async (s, e) =>
                            {
                                e.Handled = true;
                                string val = row != null ? row[capturedCol] : string.Empty;
                                if (string.IsNullOrWhiteSpace(val) || val.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return;
                                if (_viewModel != null)
                                {
                                    await _viewModel.NavigateToForeignKeyAsync(capturedCol, val);
                                }
                            };

                            Grid.SetColumn(linkPill, 0);
                            cellGrid.Children.Add(linkPill);

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
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };
                            arrowIcon[!Avalonia.Controls.PathIcon.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppTextSecondaryBrush");
                            arrowBtn.Content = arrowIcon;

                            arrowBtn.Click += async (s, e) =>
                            {
                                e.Handled = true;
                                string val = row != null ? row[capturedCol] : string.Empty;
                                if (string.IsNullOrWhiteSpace(val) || val.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return;
                                if (_viewModel != null)
                                {
                                    await _viewModel.NavigateToForeignKeyAsync(capturedCol, val);
                                }
                            };

                            Grid.SetColumn(arrowBtn, 1);
                            cellGrid.Children.Add(arrowBtn);

                            return cellGrid;
                        });

                        fkCol.CellStyleClasses.Add("fk-col");
                        fkCol.CellStyleClasses.Add(stripeClass);
                        ResultsDataGrid.Columns.Add(fkCol);
                        continue;
                    }

                    var col = new DataGridTemplateColumn
                    {
                        Tag = colName,
                        CanUserResize = true,
                        CanUserSort = true,
                        SortMemberPath = $"['{colName}']",
                        Header = BuildExcelColumnHeader(colName, isPk, fkRel: null, showDataTypes)
                    };

                    string currentCol = colName;
                    bool isStatusCol = currentCol.Equals("STATUS", StringComparison.OrdinalIgnoreCase);

                    col.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<QueryResultRow>((row, ns) =>
                    {
                        if (isStatusCol)
                        {
                            var badgeBorder = new Border
                            {
                                CornerRadius = new CornerRadius(999),
                                Padding = new Thickness(8, 2),
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };
                            var badgeText = new TextBlock
                            {
                                FontSize = 11,
                                FontWeight = FontWeight.Medium,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            };
                            badgeBorder.Child = badgeText;

                            void UpdateBadge(object? dc)
                            {
                                if (dc is QueryResultRow r)
                                {
                                    string val = r[currentCol];
                                    badgeText.Text = val;
                                    if (val.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                    {
                                        badgeBorder.Background = Brush.Parse("#15281F");
                                        badgeText.Foreground = Brush.Parse("#3DD68C");
                                    }
                                    else if (val.Equals("Inactive", StringComparison.OrdinalIgnoreCase) || val.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
                                    {
                                        badgeBorder.Background = Brush.Parse("#2F161C");
                                        badgeText.Foreground = Brush.Parse("#F2677A");
                                    }
                                    else if (val.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                                    {
                                        badgeBorder.Background = Brush.Parse("#2B2314");
                                        badgeText.Foreground = Brush.Parse("#F59E0B");
                                    }
                                    else
                                    {
                                        badgeBorder.Background = Brushes.Transparent;
                                        badgeText[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppTableCellForegroundBrush");
                                    }
                                }
                                else
                                {
                                    badgeText.Text = string.Empty;
                                    badgeBorder.Background = Brushes.Transparent;
                                }
                            }

                            UpdateBadge(row);
                            badgeBorder.DataContextChanged += (s, e) => UpdateBadge(badgeBorder.DataContext);
                            return badgeBorder;
                        }

                        var textBlock = new TextBlock
                        {
                            FontSize = 11,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new Thickness(8, 2)
                        };
                        textBlock[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("AppTableCellForegroundBrush");

                        void UpdateText(object? dc)
                        {
                            if (dc is QueryResultRow r)
                            {
                                string val = r[currentCol];
                                textBlock.Text = val;
                                if (val.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                                {
                                    textBlock.Opacity = 0.45;
                                    textBlock.FontStyle = FontStyle.Italic;
                                }
                                else
                                {
                                    textBlock.Opacity = 1.0;
                                    textBlock.FontStyle = FontStyle.Normal;
                                }
                            }
                            else
                            {
                                textBlock.Text = string.Empty;
                            }
                        }

                        UpdateText(row);
                        textBlock.DataContextChanged += (s, e) => UpdateText(textBlock.DataContext);
                        return textBlock;
                    });

                    if (isPk)
                    {
                        col.CellStyleClasses.Add("pk-col");
                    }
                    col.CellStyleClasses.Add(stripeClass);
                    ResultsDataGrid.Columns.Add(col);
                }

                _cachedDataColumnIndexes = ResultsDataGrid.Columns
                    .Where(column => !IsRowHeader(column))
                    .Select(column => column.DisplayIndex)
                    .OrderBy(index => index)
                    .ToList();
                _dataMinCol = _cachedDataColumnIndexes.Count > 0 ? _cachedDataColumnIndexes[0] : 1;
                _dataMaxCol = _cachedDataColumnIndexes.Count > 0 ? _cachedDataColumnIndexes[^1] : 1;
                UpdateSpanCache();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QueryResultTableComponent] Error updating columns: {ex}");
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

    private StackPanel BuildExcelColumnHeader(
        string colName,
        bool isPk,
        ForeignKeyRelation? fkRel,
        bool showDataTypes)
    {
        var headerPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var tip = _viewModel?.GetColumnHeaderTooltip(colName);
        if (!string.IsNullOrWhiteSpace(tip))
        {
            ToolTip.SetTip(headerPanel, tip);
        }

        if (isPk)
        {
            var pkBadge = new Border
            {
                Background = ResolveThemeBrush("AppTablePkBadgeBackgroundBrush", "#F1F5F9"),
                BorderBrush = ResolveThemeBrush("AppTablePkBadgeBorderBrush", "#CBD5E1"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Padding = new Avalonia.Thickness(3, 1),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var pkStack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2 };
            pkStack.Children.Add(new Avalonia.Controls.PathIcon
            {
                Data = Avalonia.Media.Geometry.Parse("M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z"),
                Width = 9,
                Height = 9,
                Foreground = ResolveThemeBrush("AppTablePkBadgeForegroundBrush", "#64748B"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            pkStack.Children.Add(new TextBlock
            {
                Text = "PK",
                FontSize = 9,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = ResolveThemeBrush("AppTablePkBadgeForegroundBrush", "#64748B"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            pkBadge.Child = pkStack;
            headerPanel.Children.Add(pkBadge);
        }
        else if (fkRel != null)
        {
            var fkBadge = new Border
            {
                Background = ResolveThemeBrush("AppTableFkBadgeBackgroundBrush", "#F1F5F9"),
                BorderBrush = ResolveThemeBrush("AppTableFkBadgeBorderBrush", "#CBD5E1"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Padding = new Avalonia.Thickness(3, 1),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var fkStack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2 };
            fkStack.Children.Add(new Avalonia.Controls.PathIcon
            {
                Data = Avalonia.Media.Geometry.Parse("M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"),
                Width = 9,
                Height = 9,
                Foreground = ResolveThemeBrush("AppTableFkBadgeForegroundBrush", "#475569"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            fkStack.Children.Add(new TextBlock
            {
                Text = "FK",
                FontSize = 9,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = ResolveThemeBrush("AppTableFkBadgeForegroundBrush", "#475569"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            fkBadge.Child = fkStack;
            headerPanel.Children.Add(fkBadge);
        }

        headerPanel.Children.Add(new TextBlock
        {
            Text = colName.ToUpperInvariant(),
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = ResolveThemeBrush("AppTableHeaderForegroundBrush", "#0F172A"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        if (showDataTypes)
        {
            string? dataType = _viewModel?.GetColumnDataType(colName);
            if (!string.IsNullOrWhiteSpace(dataType))
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = dataType.ToLowerInvariant(),
                    FontSize = 9.5,
                    FontWeight = Avalonia.Media.FontWeight.Normal,
                    Opacity = 0.72,
                    Foreground = ResolveThemeBrush("AppTextMutedBrush", "#64748B"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
            }
        }

        return headerPanel;
    }

    private IBrush ResolveThemeBrush(string key, string fallbackHex)
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var res) && res is IBrush b)
        {
            return b;
        }
        if (this.TryFindResource(key, out var localRes) && localRes is IBrush lb)
        {
            return lb;
        }
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        try
        {
            if (e.Row.DataContext is not QueryResultRow item || e.Column == null)
                return;

            var point = e.PointerPressedEventArgs.GetCurrentPoint(ResultsDataGrid);
            var extend = e.PointerPressedEventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var header = IsRowHeader(e.Column);

            if (point.Properties.IsRightButtonPressed)
            {
                // Commit selection on press; the ContextMenu itself opens on PointerReleased
                // so releasing the right button cannot dismiss or mis-click the popup.
                var alreadySelected = header
                    ? IsRowFullySelected(item)
                    : IsCellSelected(item, e.Column.DisplayIndex);
                if (!alreadySelected)
                    BeginPointerSelection(item, e.Column, extend: false);
                else
                    _selectedViaRowHeader = header;

                string? colName = header ? null : ExtractHeaderName(e.Column);
                var cellCtrl = FindCellControl(item, e.Column);
                _pendingRightClickTarget = new GridContextTarget(
                    item,
                    colName,
                    header || string.IsNullOrWhiteSpace(colName),
                    CanCopy: true,
                    cellCtrl ?? (Control)ResultsDataGrid);

                ResultsDataGrid.Focus();
                return;
            }

            _pendingRightClickTarget = null;
            if (!point.Properties.IsLeftButtonPressed)
                return;

            if (e.PointerPressedEventArgs.ClickCount == 2 && !header)
            {
                string colName = ExtractHeaderName(e.Column);
                if (_viewModel != null && !string.IsNullOrEmpty(colName) && _viewModel.IsColumnForeignKey(colName))
                {
                    e.PointerPressedEventArgs.Handled = true;
                    string val = item[colName];
                    if (!string.IsNullOrWhiteSpace(val) && !val.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        _ = _viewModel.NavigateToForeignKeyAsync(colName, val);
                    }
                    return;
                }

                _viewModel?.OpenEditCell(item, colName);
                e.PointerPressedEventArgs.Handled = true;
                return;
            }

            BeginPointerSelection(item, e.Column, extend);
            _dragging = true;
            _capturedPointer = e.PointerPressedEventArgs.Pointer;
            _capturedPointer?.Capture(ResultsDataGrid);
            ResultsDataGrid.Focus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QueryResultTableComponent] OnCellPointerPressed error: {ex.Message}");
        }
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
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            var pending = _pendingRightClickTarget;
            _pendingRightClickTarget = null;

            var target = pending ?? ResolveGridContext(
                new ContextMenuRequest(ResultsDataGrid, e.Source as Visual, IsPointerTriggered: true, e.GetPosition(ResultsDataGrid)));

            if (target != null &&
                ContextMenuService.Show(
                    ResultsDataGrid,
                    target,
                    ConfigureGridContextMenu,
                    target.PlacementControl ?? ResultsDataGrid,
                    isPointer: true))
            {
                e.Handled = true;
            }
            return;
        }

        if (!_dragging)
            return;

        _dragging = false;
        _dragRows = false;
        _pendingRightClickTarget = null;
        _capturedPointer?.Capture(null);
        _capturedPointer = null;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        ApplyRow(e.Row);
    }

    private void OnUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        foreach (var cell in e.Row.GetVisualDescendants().OfType<DataGridCell>())
        {
            if (cell.Classes.Contains(SelectedClass))
                cell.Classes.Remove(SelectedClass);
            if (cell.Classes.Contains(ActiveClass))
                cell.Classes.Remove(ActiveClass);
        }
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateSpanCache();
            RefreshVisuals();
        }, DispatcherPriority.Background);
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

        if (ctrl && e.Key == Key.V)
        {
            if (!_selectedViaRowHeader && _viewModel?.HasRows == true && _viewModel.PasteCellCommand.CanExecute(null))
            {
                _ = _viewModel.PasteCellCommand.ExecuteAsync(null);
                e.Handled = true;
            }
            return;
        }

        if (ctrl && e.Key == Key.A)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 || e.Key == Key.F12)
        {
            if (_selectedViaRowHeader || _viewModel == null || !_viewModel.HasRows)
            {
                return;
            }

            var targetRow = _activeRow ?? _viewModel.SelectedRow;
            var colHeader = ExtractHeaderName(ResultsDataGrid.CurrentColumn ?? ResultsDataGrid.Columns.ElementAtOrDefault(_activeCol));
            if (string.IsNullOrWhiteSpace(colHeader) || colHeader == "ROW #")
            {
                return;
            }

            _viewModel.OpenEditCell(targetRow, colHeader);
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

        _selectedViaRowHeader = false;
        e.Handled = true;
    }

    private void BeginPointerSelection(QueryResultRow item, DataGridColumn column, bool extend)
    {
        if (IsRowHeader(column))
        {
            _dragRows = true;
            _selectedViaRowHeader = true;
            if (extend && _anchorRow != null)
                SelectRowSpan(_anchorRow, item, scroll: false);
            else
                SelectRowSpan(item, item, scroll: false);
            return;
        }

        _dragRows = false;
        _selectedViaRowHeader = false;
        if (extend && _anchorRow != null)
            ExtendTo(item, column.DisplayIndex, scroll: false);
        else
            SelectCell(item, column.DisplayIndex, scroll: false);
    }

    private void SelectCell(QueryResultRow row, int column, bool scroll)
    {
        _selectedViaRowHeader = false;
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

        _selectedViaRowHeader = false;
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
        UpdateSpanCache();
        SyncGridCurrency(scroll);
        QueueRefresh();
        UpdateStatus();
    }

    private void UpdateSpanCache()
    {
        if (!_hasSelection || _cornerARow == null || _cornerBRow == null || ResultsDataGrid.CollectionView is not DataGridCollectionView view)
        {
            _spanValid = false;
            _spanRowStart = _spanRowEnd = -1;
            return;
        }

        int first = view.IndexOf(_cornerARow);
        int second = view.IndexOf(_cornerBRow);
        if (first >= 0 && second >= 0)
        {
            _spanRowStart = Math.Min(first, second);
            _spanRowEnd = Math.Max(first, second);
            _spanValid = true;
        }
        else
        {
            _spanValid = false;
            _spanRowStart = _spanRowEnd = -1;
        }
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
        }, DispatcherPriority.Render);
    }

    private void RefreshVisuals()
    {
        foreach (var row in ResultsDataGrid.GetVisualDescendants().OfType<DataGridRow>())
            ApplyRow(row);

        foreach (var colHeader in ResultsDataGrid.GetVisualDescendants().OfType<DataGridColumnHeader>())
        {
            var matchedCol = ResultsDataGrid.Columns.FirstOrDefault(c =>
                !IsRowHeader(c) && ReferenceEquals(c.Header, colHeader.Content));
            bool isColActive = _hasSelection && matchedCol != null &&
                matchedCol.DisplayIndex >= _col1 && matchedCol.DisplayIndex <= _col2;

            if (isColActive)
            {
                if (!colHeader.Classes.Contains(HeaderActiveClass))
                    colHeader.Classes.Add(HeaderActiveClass);
            }
            else
            {
                if (colHeader.Classes.Contains(HeaderActiveClass))
                    colHeader.Classes.Remove(HeaderActiveClass);
            }
        }
    }

    private void ApplyRow(DataGridRow row)
    {
        if (row?.DataContext is not QueryResultRow item)
            return;

        try
        {
            bool isEven = row.Index % 2 == 0;
            if (isEven)
            {
                if (!row.Classes.Contains(RowEvenClass)) row.Classes.Add(RowEvenClass);
                if (row.Classes.Contains(RowOddClass)) row.Classes.Remove(RowOddClass);
            }
            else
            {
                if (!row.Classes.Contains(RowOddClass)) row.Classes.Add(RowOddClass);
                if (row.Classes.Contains(RowEvenClass)) row.Classes.Remove(RowEvenClass);
            }

            if (!_hasSelection)
            {
                foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>())
                {
                    if (cell.Classes.Contains(SelectedClass))
                        cell.Classes.Remove(SelectedClass);
                    if (cell.Classes.Contains(ActiveClass))
                        cell.Classes.Remove(ActiveClass);
                    if (cell.Classes.Contains(GutterActiveClass))
                        cell.Classes.Remove(GutterActiveClass);
                }
                return;
            }

            int rowIndex = row.Index;
            bool isRowInSpan = _spanValid && rowIndex >= _spanRowStart && rowIndex <= _spanRowEnd;
            bool isRowFullySelected = isRowInSpan && _col1 <= _dataMinCol && _col2 >= _dataMaxCol;
            bool isRowActive = ReferenceEquals(item, _activeRow);

            foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>())
            {
                var column = ColumnOf(cell);
                if (column == null)
                {
                    if (cell.Classes.Contains(SelectedClass))
                        cell.Classes.Remove(SelectedClass);
                    if (cell.Classes.Contains(ActiveClass))
                        cell.Classes.Remove(ActiveClass);
                    if (cell.Classes.Contains(GutterActiveClass))
                        cell.Classes.Remove(GutterActiveClass);
                    continue;
                }

                int colIndex = column.DisplayIndex;
                bool header = IsRowHeader(column);
                bool active = !header && isRowActive && colIndex == _activeCol;
                bool selected = !active && (header
                    ? isRowFullySelected
                    : (isRowInSpan && colIndex >= _col1 && colIndex <= _col2));

                if (header)
                {
                    bool gutterActive = isRowInSpan || isRowActive;
                    if (gutterActive)
                    {
                        if (!cell.Classes.Contains(GutterActiveClass))
                            cell.Classes.Add(GutterActiveClass);
                    }
                    else
                    {
                        if (cell.Classes.Contains(GutterActiveClass))
                            cell.Classes.Remove(GutterActiveClass);
                    }
                }

                if (selected)
                {
                    if (!cell.Classes.Contains(SelectedClass))
                        cell.Classes.Add(SelectedClass);
                }
                else
                {
                    if (cell.Classes.Contains(SelectedClass))
                        cell.Classes.Remove(SelectedClass);
                }

                if (active)
                {
                    if (!cell.Classes.Contains(ActiveClass))
                        cell.Classes.Add(ActiveClass);
                }
                else
                {
                    if (cell.Classes.Contains(ActiveClass))
                        cell.Classes.Remove(ActiveClass);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QueryResultTableComponent] ApplyRow error: {ex.Message}");
        }
    }

    private void UpdateStatus()
    {
        if (_viewModel == null)
            return;

        if (!TryGetSpan(out var r1, out var r2, out var c1, out var c2))
        {
            _viewModel.SelectionStatus = "Click a cell to select it · drag or Shift-click for a range";
            _viewModel.ClearActiveCellInfo();
            return;
        }

        var rows = r2 - r1 + 1;
        var selectedDataCols = ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column) && column.DisplayIndex >= c1 && column.DisplayIndex <= c2)
            .OrderBy(column => column.DisplayIndex)
            .ToList();
        int cols = selectedDataCols.Count;

        _viewModel.SelectionStatus = rows == 1 && cols == 1
            ? "1 cell selected"
            : $"{rows}R × {cols}C selected";

        var dataIndexes = DataColumnIndexes();
        int oneBasedExcelCol = dataIndexes.IndexOf(_activeCol) + 1;
        string colLetter = oneBasedExcelCol > 0 ? QueryResultTableViewModel.ToExcelColumnName(oneBasedExcelCol) : "?";
        int rowNumber = _activeRow != null ? _activeRow.RowIndex : (r1 + 1);
        string cellRef = $"{colLetter}{rowNumber}";

        var activeColObj = ColumnAtDisplayIndex(_activeCol);
        string activeColName = activeColObj != null ? ExtractHeaderName(activeColObj) : string.Empty;
        string activeVal = (_activeRow != null && !string.IsNullOrEmpty(activeColName))
            ? _activeRow[activeColName]
            : string.Empty;

        string statsText = string.Empty;
        if (_viewModel.ShowExcelStatusStats && (rows * cols) > 1 &&
            ResultsDataGrid.CollectionView is DataGridCollectionView view &&
            (rows * cols) <= 10_000)
        {
            int nonNullCount = 0;
            int numericCount = 0;
            double sum = 0;

            for (int ri = r1; ri <= r2 && ri < view.Count; ri++)
            {
                if (view.GetItemAt(ri) is not QueryResultRow rowItem) continue;
                foreach (var col in selectedDataCols)
                {
                    string name = ExtractHeaderName(col);
                    if (string.IsNullOrEmpty(name)) continue;
                    string v = rowItem[name];
                    if (string.IsNullOrWhiteSpace(v) || v.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                        continue;

                    nonNullCount++;
                    if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        numericCount++;
                        sum += num;
                    }
                }
            }

            if (numericCount > 0)
            {
                double avg = sum / numericCount;
                statsText = $"Avg: {avg:0.##}  |  Count: {nonNullCount}  |  Sum: {sum:0.##}";
            }
            else if (nonNullCount > 0)
            {
                statsText = $"Count: {nonNullCount}";
            }
        }

        _viewModel.UpdateActiveCellInfo(cellRef, activeColName, activeVal, statsText);
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
        if (!_hasSelection || !_spanValid)
        {
            rowStart = rowEnd = columnStart = columnEnd = 0;
            return false;
        }

        rowStart = _spanRowStart;
        rowEnd = _spanRowEnd;
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

        return columnStart <= _dataMinCol && columnEnd >= _dataMaxCol;
    }

    private (QueryResultRow Item, DataGridColumn Column)? HitTestCell(PointerEventArgs e) =>
        HitTestCellAt(e.GetPosition(ResultsDataGrid)) is { } hit ? (hit.Item, hit.Column) : null;

    private (QueryResultRow Item, DataGridColumn Column, DataGridCell Cell)? HitTestCellAt(Point position)
    {
        if (ResultsDataGrid.InputHitTest(position) is not Visual visual)
            return null;

        if (visual.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) != null)
            return null;

        var cell = visual as DataGridCell ?? visual.FindAncestorOfType<DataGridCell>();
        if (cell == null)
            return null;

        var row = DataGridRow.GetRowContainingElement(cell);
        if (row?.DataContext is not QueryResultRow item)
            return null;

        var column = ColumnOf(cell);
        return column == null ? null : (item, column, cell);
    }

    internal GridContextTarget? ResolveGridContext(ContextMenuRequest request)
    {
        if (_viewModel == null || !_viewModel.HasRows)
            return null;

        if (request.IsPointerTriggered && request.Position is Point pos)
        {
            if (HitTestCellAt(pos) is not { } hit)
                return null;

            bool header = IsRowHeader(hit.Column);
            bool alreadySelected = header
                ? IsRowFullySelected(hit.Item)
                : IsCellSelected(hit.Item, hit.Column.DisplayIndex);

            if (!alreadySelected)
            {
                BeginPointerSelection(hit.Item, hit.Column, extend: false);
            }
            else
            {
                _selectedViaRowHeader = header;
            }

            string? colName = header ? null : ExtractHeaderName(hit.Column);
            return new GridContextTarget(hit.Item, colName, header || string.IsNullOrWhiteSpace(colName), CanCopy: true, hit.Cell);
        }

        var activeRow = _activeRow ?? ResultsDataGrid.SelectedItem as QueryResultRow ?? _viewModel.SelectedRow;
        if (activeRow == null)
            return null;

        var activeCol = ColumnAtDisplayIndex(_activeCol) ?? ResultsDataGrid.CurrentColumn;
        bool rowHeaderOnly = _selectedViaRowHeader || (activeCol != null && IsRowHeader(activeCol));
        string? activeColName = !rowHeaderOnly && activeCol != null ? ExtractHeaderName(activeCol) : null;
        var placementCell = FindCellControl(activeRow, activeCol);

        return new GridContextTarget(
            activeRow,
            activeColName,
            rowHeaderOnly || string.IsNullOrWhiteSpace(activeColName),
            CanCopy: true,
            placementCell ?? (Control)ResultsDataGrid);
    }

    internal void ConfigureGridContextMenu(ContextMenuBuilder<GridContextTarget> builder, GridContextTarget context)
    {
        bool CanMutateCell(GridContextTarget ctx) =>
            _viewModel?.HasRows == true && !ctx.IsRowHeaderOnly && ctx.Row != null && !string.IsNullOrWhiteSpace(ctx.ColumnName);

        builder
            .AddItem(
                header: "Edit Cell",
                command: _viewModel?.OpenEditSelectedCellCommand,
                symbol: FASymbol.Edit,
                gesture: "F2",
                isEnabled: CanMutateCell)
            .AddItem(
                header: "Copy",
                command: _viewModel?.CopyCellCommand,
                symbol: FASymbol.Copy,
                gesture: "Ctrl+C",
                isEnabled: ctx => _viewModel?.HasRows == true && ctx.CanCopy)
            .AddItem(
                header: "Paste",
                command: _viewModel?.PasteCellCommand,
                symbol: FASymbol.Paste,
                gesture: "Ctrl+V",
                isEnabled: CanMutateCell);
    }

    private Control? FindCellControl(QueryResultRow row, DataGridColumn? column)
    {
        if (column == null)
            return null;

        foreach (var gridRow in ResultsDataGrid.GetVisualDescendants().OfType<DataGridRow>())
        {
            if (!ReferenceEquals(gridRow.DataContext, row))
                continue;

            foreach (var cell in gridRow.GetVisualDescendants().OfType<DataGridCell>())
            {
                if (ReferenceEquals(ColumnOf(cell), column))
                    return cell;
            }
        }

        return null;
    }

    private IReadOnlyList<PasteCellSlot> ResolvePasteTargets(int matrixRows, int matrixCols)
    {
        if (_selectedViaRowHeader)
            return Array.Empty<PasteCellSlot>();

        if (ResultsDataGrid.CollectionView is not DataGridCollectionView view || view.Count == 0)
            return Array.Empty<PasteCellSlot>();

        var orderedDataColumns = ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column))
            .OrderBy(column => column.DisplayIndex)
            .ToList();

        if (orderedDataColumns.Count == 0)
            return Array.Empty<PasteCellSlot>();

        if (matrixRows == 1 && matrixCols == 1)
        {
            if (TryGetSpan(out var r1, out var r2, out var c1, out var c2))
            {
                var selectedCols = orderedDataColumns
                    .Where(column => column.DisplayIndex >= c1 && column.DisplayIndex <= c2)
                    .ToList();
                if (selectedCols.Count == 0)
                    return Array.Empty<PasteCellSlot>();

                var slots = new List<PasteCellSlot>();
                for (int r = r1; r <= r2 && r < view.Count; r++)
                {
                    if (view.GetItemAt(r) is not QueryResultRow row)
                        continue;

                    foreach (var col in selectedCols)
                    {
                        string colName = ExtractHeaderName(col);
                        if (!string.IsNullOrWhiteSpace(colName))
                        {
                            slots.Add(new PasteCellSlot(row, colName, 0, 0));
                        }
                    }
                }
                return slots;
            }

            var singleRow = _activeRow ?? ResultsDataGrid.SelectedItem as QueryResultRow;
            var singleCol = ColumnAtDisplayIndex(_activeCol) ?? ResultsDataGrid.CurrentColumn;
            if (singleRow == null || singleCol == null || IsRowHeader(singleCol))
                return Array.Empty<PasteCellSlot>();

            string singleColName = ExtractHeaderName(singleCol);
            return string.IsNullOrWhiteSpace(singleColName)
                ? Array.Empty<PasteCellSlot>()
                : new[] { new PasteCellSlot(singleRow, singleColName, 0, 0) };
        }

        int startRowIdx;
        int startDataColIdx;

        if (TryGetSpan(out var spanRowStart, out _, out var spanColStart, out _))
        {
            startRowIdx = spanRowStart;
            startDataColIdx = orderedDataColumns.FindIndex(c => c.DisplayIndex >= spanColStart);
        }
        else
        {
            var startRow = _activeRow ?? ResultsDataGrid.SelectedItem as QueryResultRow;
            startRowIdx = startRow != null ? view.IndexOf(startRow) : -1;
            var startCol = ColumnAtDisplayIndex(_activeCol) ?? ResultsDataGrid.CurrentColumn;
            startDataColIdx = startCol != null && !IsRowHeader(startCol)
                ? orderedDataColumns.IndexOf(startCol)
                : -1;
        }

        if (startRowIdx < 0 || startDataColIdx < 0)
            return Array.Empty<PasteCellSlot>();

        var matrixSlots = new List<PasteCellSlot>();
        for (int r = 0; r < matrixRows && startRowIdx + r < view.Count; r++)
        {
            if (view.GetItemAt(startRowIdx + r) is not QueryResultRow targetRow)
                continue;

            for (int c = 0; c < matrixCols && startDataColIdx + c < orderedDataColumns.Count; c++)
            {
                string colName = ExtractHeaderName(orderedDataColumns[startDataColIdx + c]);
                if (!string.IsNullOrWhiteSpace(colName))
                {
                    matrixSlots.Add(new PasteCellSlot(targetRow, colName, r, c));
                }
            }
        }

        return matrixSlots;
    }

    private DataGridColumn? ColumnOf(DataGridCell cell)
    {
        if (OwningColumnGetter != null)
        {
            var col = OwningColumnGetter(cell);
            if (col != null) return col;
        }

        if (OwningColumnProperty?.GetValue(cell) is DataGridColumn owning)
        {
            return owning;
        }

        return null;
    }

    private DataGridColumn? ColumnAtDisplayIndex(int displayIndex) =>
        ResultsDataGrid.Columns.FirstOrDefault(column => column.DisplayIndex == displayIndex);

    private List<int> DataColumnIndexes()
    {
        if (_cachedDataColumnIndexes.Count > 0)
            return _cachedDataColumnIndexes;

        _cachedDataColumnIndexes = ResultsDataGrid.Columns
            .Where(column => !IsRowHeader(column))
            .Select(column => column.DisplayIndex)
            .OrderBy(index => index)
            .ToList();
        return _cachedDataColumnIndexes;
    }

    private static bool IsRowHeader(DataGridColumn column) =>
        (column.Tag is string tag && tag == "ROW #") ||
        column.Header?.ToString() == "ROW #" ||
        column.DisplayIndex == 0;

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
        if (column.Tag is string tag && !string.IsNullOrEmpty(tag))
        {
            return tag;
        }
        if (column.Header is StackPanel sp)
        {
            var tb = sp.Children.OfType<TextBlock>().LastOrDefault();
            return tb?.Text ?? string.Empty;
        }
        return column.Header?.ToString() ?? string.Empty;
    }

    private static string CellText(QueryResultRow row, DataGridColumn column)
    {
        var header = ExtractHeaderName(column);
        if (header == "ROW #") return row.RowIndex.ToString();

        var value = !string.IsNullOrEmpty(header) ? row[header] : string.Empty;

        if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }
}
