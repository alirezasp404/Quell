using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Quell.ViewModels;
using Quell.Views;

namespace Quell;

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
                Services.DatabaseStorageService.Instance.PreloadStartupDataAsync().GetAwaiter().GetResult();

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
            Services.DatabaseStorageService.Instance.PreloadStartupDataAsync().GetAwaiter().GetResult();

            singleView.MainView = new MainView
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
