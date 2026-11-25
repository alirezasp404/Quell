using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Quell.Services;
using Quell.ViewModels;

namespace Quell.Components;

public partial class SidebarComponent : UserControl
{
    private Point? _dragStartPoint;
    private PointerPressedEventArgs? _dragStartArgs;
    private SavedQueryItemViewModel? _dragCandidateItem;

    public SidebarComponent()
    {
        InitializeComponent();

        // ContextRequested and PointerPressed (Right-Click) for Explorer & Databases context menus
        AddHandler(ContextRequestedEvent, OnSidebarContextRequested, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnSidebarPointerPressed, RoutingStrategies.Tunnel);

        // Drag & Drop handlers for Saved Queries -> Folders
        AddHandler(DragDrop.DragOverEvent, OnSidebarDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnSidebarDragLeave);
        AddHandler(DragDrop.DropEvent, OnSidebarDrop);
    }

    private void OnSidebarContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is Visual visual && HandleContextMenuRequest(visual))
        {
            e.Handled = true;
        }
    }

    private void OnSidebarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            if (e.Source is Visual visual && HandleContextMenuRequest(visual))
            {
                e.Handled = true;
            }
        }
    }

    private void OnTableNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed && (sender as Control)?.DataContext is TableTreeNode tableNode)
            {
                if (DataContext is SidebarViewModel vm)
                {
                    if (tableNode.IsProcedure)
                    {
                        _ = vm.OpenProcedureNodeAsync(tableNode);
                    }
                    else
                    {
                        _ = vm.SelectTableNode(tableNode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarComponent] OnTableNodePointerPressed error: {ex.Message}");
        }
    }

    private void OnSavedFolderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            (sender as Control)?.DataContext is SavedQueryFolderViewModel folder)
        {
            folder.IsExpanded = !folder.IsExpanded;
        }
    }

    private void OnSavedQueryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if ((sender as Control)?.DataContext is SavedQueryItemViewModel item)
        {
            if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) != null)
                return;

            if (DataContext is SidebarViewModel vm)
            {
                vm.SelectSavedQueryItem(item);
            }

            _dragStartPoint = e.GetPosition(this);
            _dragStartArgs = e;
            _dragCandidateItem = item;
        }
    }

    private async void OnSavedQueryPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidateItem == null || _dragStartPoint == null || _dragStartArgs == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragCandidateItem = null;
            _dragStartPoint = null;
            _dragStartArgs = null;
            return;
        }

        var pos = e.GetPosition(this);
        var delta = pos - _dragStartPoint.Value;
        if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;

        var dragged = _dragCandidateItem;
        var triggerArgs = _dragStartArgs;
        _dragCandidateItem = null;
        _dragStartPoint = null;
        _dragStartArgs = null;

        try
        {
            var data = new DataTransfer();
            var dtItem = new DataTransferItem();
            dtItem.SetText(dragged.Id.ToString());
            data.Add(dtItem);
            await DragDrop.DoDragDropAsync(triggerArgs, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarComponent] DragDrop error: {ex.Message}");
        }
    }

    private void OnSidebarDragOver(object? sender, DragEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<Border>(includeSelf: true)?.DataContext is SavedQueryFolderViewModel folder)
        {
            folder.IsDragOver = true;
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void OnSidebarDragLeave(object? sender, DragEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<Border>(includeSelf: true)?.DataContext is SavedQueryFolderViewModel folder)
        {
            folder.IsDragOver = false;
        }
    }

    private async void OnSidebarDrop(object? sender, DragEventArgs e)
    {
        var border = (e.Source as Visual)?.FindAncestorOfType<Border>(includeSelf: true);
        if (border?.DataContext is SavedQueryFolderViewModel folder)
        {
            folder.IsDragOver = false;
            string? idText = e.DataTransfer.TryGetText();
            if (int.TryParse(idText, out int queryId) && DataContext is SidebarViewModel vm)
            {
                await vm.MoveSavedQueryToFolderAsync(queryId, folder.Name);
                e.Handled = true;
            }
        }
    }

    private void OnSavedQueryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SavedQueryItemViewModel item && DataContext is SidebarViewModel vm)
        {
            vm.OpenSavedQueryInNewTab(item);
        }
    }

    private async void OnSavedQueryRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as Control)?.DataContext is not SavedQueryItemViewModel item || DataContext is not SidebarViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await vm.CommitRenameSavedQueryAsync(item);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelRenameSavedQuery(item);
        }
    }

    private async void OnSavedQueryRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SavedQueryItemViewModel item &&
            item.IsRenaming &&
            DataContext is SidebarViewModel vm)
        {
            await vm.CommitRenameSavedQueryAsync(item);
        }
    }

    private void OnHistoryEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) != null)
            return;

        if ((sender as Control)?.DataContext is HistoryEntryViewModel entry && DataContext is SidebarViewModel vm)
        {
            vm.SelectHistoryEntry(entry);
        }
    }

    private bool HandleContextMenuRequest(Visual? visual)
    {
        if (visual == null) return false;

        var tvi = visual.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        var itemContext = tvi?.DataContext ?? (visual as Control)?.DataContext;

        if (itemContext is DatabaseConnectionTreeNode dbNode)
        {
            var target = (Control?)tvi ?? visual as Control ?? this;
            ShowDatabaseContextMenu(target, dbNode);
            return true;
        }

        if (itemContext is TableTreeNode tableNode)
        {
            var target = (Control?)tvi ?? visual as Control ?? this;
            ShowTableContextMenu(target, tableNode);
            return true;
        }

        var border = visual.FindAncestorOfType<Border>(includeSelf: true);
        if (border?.DataContext is ConnectionRecord connRecord)
        {
            ShowSavedConnectionContextMenu(border, connRecord);
            return true;
        }

        return false;
    }

    private void ShowDatabaseContextMenu(Control targetControl, DatabaseConnectionTreeNode dbNode)
    {
        var menu = new ContextMenu();

        if (!dbNode.IsConnected)
        {
            var connectItem = new MenuItem
            {
                Header = "Connect",
                Icon = new FASymbolIcon { Symbol = FASymbol.Play, FontSize = 12, Foreground = Brush.Parse("#4F6FFF") }
            };
            connectItem.Click += async (_, _) => await dbNode.ConnectAsync();
            menu.Items.Add(connectItem);
        }
        else
        {
            var disconnectItem = new MenuItem
            {
                Header = "Disconnect",
                Icon = new FASymbolIcon { Symbol = FASymbol.Dismiss, FontSize = 12, Foreground = Brush.Parse("#F2677A") }
            };
            disconnectItem.Click += async (_, _) => await dbNode.DisconnectAsync();
            menu.Items.Add(disconnectItem);
        }

        menu.Items.Add(new Separator());

        var editItem = new MenuItem
        {
            Header = "Edit Connection…",
            Icon = new FASymbolIcon { Symbol = FASymbol.Settings, FontSize = 12, Foreground = Brush.Parse("#8A93A3") }
        };
        editItem.Click += (_, _) => dbNode.Edit();
        menu.Items.Add(editItem);

        if (dbNode.IsConnected)
        {
            var refreshItem = new MenuItem
            {
                Header = "Refresh Tables",
                InputGesture = new KeyGesture(Key.F5),
                Icon = new FASymbolIcon { Symbol = FASymbol.Refresh, FontSize = 12, Foreground = Brush.Parse("#8A93A3") }
            };
            refreshItem.Click += async (_, _) => await dbNode.RefreshAsync();
            menu.Items.Add(refreshItem);
        }

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem
        {
            Header = "Delete Connection",
            Icon = new FASymbolIcon { Symbol = FASymbol.Delete, FontSize = 12, Foreground = Brush.Parse("#F2677A") }
        };
        deleteItem.Click += async (_, _) => await dbNode.DeleteAsync();
        menu.Items.Add(deleteItem);

        targetControl.ContextMenu = menu;
        menu.Open(targetControl);
    }

    private void ShowTableContextMenu(Control targetControl, TableTreeNode tableNode)
    {
        var menu = new ContextMenu();

        var selectTop100Item = new MenuItem
        {
            Header = "Select top 100 rows",
            InputGesture = new KeyGesture(Key.Return),
            Icon = new FASymbolIcon { Symbol = FASymbol.Play, FontSize = 12, Foreground = Brush.Parse("#4F6FFF") }
        };
        selectTop100Item.Click += async (_, _) => await tableNode.ExecuteTop100Async();
        menu.Items.Add(selectTop100Item);

        var openDataItem = new MenuItem { Header = "Open table data" };
        openDataItem.Click += async (_, _) => await tableNode.OpenTableDataAsync();
        menu.Items.Add(openDataItem);

        var viewStructureItem = new MenuItem { Header = "View structure" };
        viewStructureItem.Click += async (_, _) => await tableNode.ViewStructureAsync();
        menu.Items.Add(viewStructureItem);

        var showDiagramItem = new MenuItem { Header = "Show in schema diagram" };
        showDiagramItem.Click += async (_, _) => await tableNode.ShowInDiagramAsync();
        menu.Items.Add(showDiagramItem);

        menu.Items.Add(new Separator());

        var scriptMenu = new MenuItem { Header = "Generate script" };
        var genSelect = new MenuItem { Header = "SELECT Script" };
        genSelect.Click += async (_, _) => await tableNode.GenerateScriptAsync("SELECT");
        var genDdl = new MenuItem { Header = "CREATE TABLE (DDL)" };
        genDdl.Click += async (_, _) => await tableNode.GenerateScriptAsync("DDL");
        var genInsert = new MenuItem { Header = "INSERT Template" };
        genInsert.Click += async (_, _) => await tableNode.GenerateScriptAsync("INSERT");
        var genUpdate = new MenuItem { Header = "UPDATE Template" };
        genUpdate.Click += async (_, _) => await tableNode.GenerateScriptAsync("UPDATE");
        var genDelete = new MenuItem { Header = "DELETE Template" };
        genDelete.Click += async (_, _) => await tableNode.GenerateScriptAsync("DELETE");
        scriptMenu.Items.Add(genSelect);
        scriptMenu.Items.Add(genDdl);
        scriptMenu.Items.Add(genInsert);
        scriptMenu.Items.Add(genUpdate);
        scriptMenu.Items.Add(genDelete);
        menu.Items.Add(scriptMenu);

        var copyNameItem = new MenuItem
        {
            Header = "Copy name",
            InputGesture = new KeyGesture(Key.C, KeyModifiers.Control),
            Icon = new FASymbolIcon { Symbol = FASymbol.Copy, FontSize = 12, Foreground = Brush.Parse("#8A93A3") }
        };
        copyNameItem.Click += async (_, _) => await tableNode.CopyTableNameAsync();
        menu.Items.Add(copyNameItem);

        var copyQualItem = new MenuItem { Header = "Copy qualified name" };
        copyQualItem.Click += async (_, _) => await tableNode.CopyQualifiedNameAsync();
        menu.Items.Add(copyQualItem);

        menu.Items.Add(new Separator());

        var refreshItem = new MenuItem
        {
            Header = "Refresh",
            InputGesture = new KeyGesture(Key.F5)
        };
        refreshItem.Click += async (_, _) => await tableNode.DatabaseNode.RefreshAsync();
        menu.Items.Add(refreshItem);

        if (tableNode.IsTable)
        {
            var truncateItem = new MenuItem
            {
                Header = "Truncate table…",
                Foreground = Brush.Parse("#F2677A")
            };
            truncateItem.Click += async (_, _) => await tableNode.TruncateTableAsync();
            menu.Items.Add(truncateItem);
        }

        targetControl.ContextMenu = menu;
        menu.Open(targetControl);
    }

    private void ShowSavedConnectionContextMenu(Control targetControl, ConnectionRecord record)
    {
        if (DataContext is not SidebarViewModel vm) return;

        var menu = new ContextMenu();

        if (!record.IsActive)
        {
            var connectItem = new MenuItem
            {
                Header = "Connect",
                Icon = new FASymbolIcon { Symbol = FASymbol.Play, FontSize = 12, Foreground = Brush.Parse("#4F6FFF") }
            };
            connectItem.Click += async (_, _) => await vm.ActivateConnection(record);
            menu.Items.Add(connectItem);
        }
        else
        {
            var disconnectItem = new MenuItem
            {
                Header = "Disconnect",
                Icon = new FASymbolIcon { Symbol = FASymbol.Dismiss, FontSize = 12, Foreground = Brush.Parse("#F2677A") }
            };
            disconnectItem.Click += async (_, _) => await vm.DisconnectSavedConnection(record);
            menu.Items.Add(disconnectItem);
        }

        menu.Items.Add(new Separator());

        var editItem = new MenuItem
        {
            Header = "Edit Connection…",
            Icon = new FASymbolIcon { Symbol = FASymbol.Settings, FontSize = 12, Foreground = Brush.Parse("#8A93A3") }
        };
        editItem.Click += (_, _) => vm.EditSavedConnection(record);
        menu.Items.Add(editItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem
        {
            Header = "Delete Connection",
            Icon = new FASymbolIcon { Symbol = FASymbol.Delete, FontSize = 12, Foreground = Brush.Parse("#F2677A") }
        };
        deleteItem.Click += async (_, _) => await vm.DisconnectAndRemoveConnection(record);
        menu.Items.Add(deleteItem);

        targetControl.ContextMenu = menu;
        menu.Open(targetControl);
    }

    private void OnSavedConnectionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ConnectionRecord record && DataContext is SidebarViewModel vm)
        {
            _ = vm.ActivateConnection(record);
        }
    }
}
