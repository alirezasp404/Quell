using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Fdb.ViewModels;
using Fdb.Views;

namespace Fdb;

public partial class App : Application
{
    public static bool DisableSplash { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (DisableSplash || desktop.Args?.Contains("--no-splash") == true)
            {
                var theme = Services.DatabaseStorageService.Instance.GetThemeAsync().GetAwaiter().GetResult();
                Services.ThemeService.Instance.ApplyTheme(theme);
                var keywordColor = Services.DatabaseStorageService.Instance.GetKeywordColorAsync().GetAwaiter().GetResult();
                Services.ThemeService.Instance.SetKeywordColor(keywordColor);
                var panelCustom = Services.DatabaseStorageService.Instance.GetPanelCustomizationAsync().GetAwaiter().GetResult();
                Services.ThemeService.Instance.ApplyPanelCustomization(panelCustom);
                Services.ShortcutService.Instance.InitializeAsync().GetAwaiter().GetResult();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
            }
            else
            {
                var splash = new SplashScreenWindow();
                desktop.MainWindow = splash;
                splash.Show();

                _ = Task.Run(async () =>
                {
                    // Execute startup SQL queries and initialize SQLite engine
                    await splash.RunStartupSequenceAsync();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var mainWindow = new MainWindow
                        {
                            DataContext = new MainViewModel(),
                        };

                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        splash.Close();
                    });
                });
            }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var theme = Services.DatabaseStorageService.Instance.GetThemeAsync().GetAwaiter().GetResult();
            Services.ThemeService.Instance.ApplyTheme(theme);
            var keywordColor = Services.DatabaseStorageService.Instance.GetKeywordColorAsync().GetAwaiter().GetResult();
            Services.ThemeService.Instance.SetKeywordColor(keywordColor);

            singleView.MainView = new MainView
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
