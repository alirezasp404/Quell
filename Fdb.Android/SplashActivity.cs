using Android.App;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace Fdb.Android;

[Activity(
    Theme = "@style/MyTheme.Splash",
    MainLauncher = false,
    NoHistory = true)]
public class SplashActivity : AvaloniaSplashActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
    }
}
