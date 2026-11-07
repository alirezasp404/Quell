using System;
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
                    try
                    {
                        // 1. Real Phase: Storage, schema, graphics, appearance, shortcuts & keywords (0% -> 88%)
                        await splash.RunStartupSequenceAsync(completeTo100: false);

                        // 2. Real Phase: Constructing Studio Workspace (89% -> 94%)
                        splash.UpdateProgress(89, "Building Studio Workspace...", "Initializing SQL Editor & IntelliSense engine...");

                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            try
                            {
                                var vm = new MainViewModel();

                                // 3. Real Phase: Building MainWindow & UI Shell (95% -> 99%)
                                splash.UpdateProgress(95, "Rendering Desktop Interface...", "Binding controls, layout grids and tabs...");
                                await Task.Delay(45);

                                var mainWindow = new MainWindow
                                {
                                    DataContext = vm,
                                };

                                // 4. Real Phase: Finalizing Studio Environment (100%)
                                splash.UpdateProgress(100, "Studio Environment Ready", "Quell v1.0.0 Ready");
                                await Task.Delay(140);

                                desktop.MainWindow = mainWindow;
                                mainWindow.Show();
                                splash.Close();
                            }
                            catch (Exception uiEx)
                            {
                                Console.WriteLine($"[App Startup UI Error]: {uiEx}");
                                splash.Close();
                                throw;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[App Startup Error]: {ex}");
                        await Dispatcher.UIThread.InvokeAsync(() => splash.Close());
                    }
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
