using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Fdb.Services;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class SidebarComponent : UserControl
{
    public SidebarComponent()
    {
        InitializeComponent();

        // Listen for ContextRequested and PointerPressed (Right-Click) across the entire sidebar panel using Tunneling
        AddHandler(ContextRequestedEvent, OnSidebarContextRequested, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnSidebarPointerPressed, RoutingStrategies.Tunnel);
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

    private bool HandleContextMenuRequest(Visual? visual)
    {
        if (visual == null) return false;

        // 1. Check if clicked inside a TreeViewItem (Explorer Tab)
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

        // 2. Check if clicked inside SavedConnections list (Databases Tab)
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
                Icon = new FASymbolIcon
                {
                    Symbol = FASymbol.Play,
                    FontSize = 12,
                    Foreground = Brush.Parse("#2563EB")
                }
            };
            connectItem.Click += async (_, _) =>
            {
                await dbNode.ConnectAsync();
            };
            menu.Items.Add(connectItem);
        }
        else
        {
            var disconnectItem = new MenuItem
            {
                Header = "Disconnect",
                Icon = new FASymbolIcon
                {
                    Symbol = FASymbol.Dismiss,
                    FontSize = 12,
                    Foreground = Brush.Parse("#EF4444")
                }
            };
            disconnectItem.Click += async (_, _) =>
            {
                await dbNode.DisconnectAsync();
            };
            menu.Items.Add(disconnectItem);
        }

        menu.Items.Add(new Separator());

        var editItem = new MenuItem
        {
            Header = "Edit Connection...",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Settings,
                FontSize = 12,
                Foreground = Brush.Parse("#6B7280")
            }
        };
        editItem.Click += (_, _) =>
        {
            dbNode.Edit();
        };
        menu.Items.Add(editItem);

        if (dbNode.IsConnected)
        {
            var refreshItem = new MenuItem
            {
                Header = "Refresh Tables",
                Icon = new FASymbolIcon
                {
                    Symbol = FASymbol.Refresh,
                    FontSize = 12,
                    Foreground = Brush.Parse("#6B7280")
                }
            };
            refreshItem.Click += async (_, _) =>
            {
                await dbNode.RefreshAsync();
            };
            menu.Items.Add(refreshItem);
        }

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem
        {
            Header = "Delete Connection",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Delete,
                FontSize = 12,
                Foreground = Brush.Parse("#EF4444")
            }
        };
        deleteItem.Click += async (_, _) =>
        {
            await dbNode.DeleteAsync();
        };
        menu.Items.Add(deleteItem);

        targetControl.ContextMenu = menu;
        menu.Open(targetControl);
    }

    private void ShowTableContextMenu(Control targetControl, TableTreeNode tableNode)
    {
        var menu = new ContextMenu();

        var selectTop100Item = new MenuItem
        {
            Header = "Select TOP 100",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Play,
                FontSize = 12,
                Foreground = Brush.Parse("#2563EB")
            }
        };
        selectTop100Item.Click += async (_, _) =>
        {
            await tableNode.ExecuteTop100Async();
        };
        menu.Items.Add(selectTop100Item);

        var copyNameItem = new MenuItem
        {
            Header = "Copy Table Name",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Copy,
                FontSize = 12,
                Foreground = Brush.Parse("#6B7280")
            }
        };
        copyNameItem.Click += async (_, _) =>
        {
            await tableNode.CopyTableNameAsync();
        };
        menu.Items.Add(copyNameItem);

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
                Icon = new FASymbolIcon
                {
                    Symbol = FASymbol.Play,
                    FontSize = 12,
                    Foreground = Brush.Parse("#2563EB")
                }
            };
            connectItem.Click += async (_, _) =>
            {
                await vm.ActivateConnection(record);
            };
            menu.Items.Add(connectItem);
        }
        else
        {
            var disconnectItem = new MenuItem
            {
                Header = "Disconnect",
                Icon = new FASymbolIcon
                {
                    Symbol = FASymbol.Dismiss,
                    FontSize = 12,
                    Foreground = Brush.Parse("#EF4444")
                }
            };
            disconnectItem.Click += async (_, _) =>
            {
                await vm.DisconnectSavedConnection(record);
            };
            menu.Items.Add(disconnectItem);
        }

        menu.Items.Add(new Separator());

        var editItem = new MenuItem
        {
            Header = "Edit Connection...",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Settings,
                FontSize = 12,
                Foreground = Brush.Parse("#6B7280")
            }
        };
        editItem.Click += (_, _) =>
        {
            vm.EditSavedConnection(record);
        };
        menu.Items.Add(editItem);

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem
        {
            Header = "Delete Connection",
            Icon = new FASymbolIcon
            {
                Symbol = FASymbol.Delete,
                FontSize = 12,
                Foreground = Brush.Parse("#EF4444")
            }
        };
        deleteItem.Click += async (_, _) =>
        {
            await vm.DisconnectAndRemoveConnection(record);
        };
        menu.Items.Add(deleteItem);

        targetControl.ContextMenu = menu;
        menu.Open(targetControl);
    }

    private void OnSavedConnectionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ConnectionRecord record)
        {
            if (DataContext is SidebarViewModel vm)
            {
                _ = vm.ActivateConnection(record);
            }
        }
    }
}
