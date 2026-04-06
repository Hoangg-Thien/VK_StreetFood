using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace VK.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "vkstreetfood",
    DataHost = "poi")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "vkstreetfood",
    DataHost = "open")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "vkstreetfood",
    DataHost = ".")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ForwardDeepLinkIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ForwardDeepLinkIntent(intent);
    }

    private static void ForwardDeepLinkIntent(Intent? intent)
    {
        var data = intent?.Data;
        if (data == null)
            return;

        if (Uri.TryCreate(data.ToString(), UriKind.Absolute, out var uri))
        {
            App.TryCapturePendingFromUri(uri);
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
        }
    }
}
