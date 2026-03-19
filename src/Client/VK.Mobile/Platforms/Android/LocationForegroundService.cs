using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using VK.Mobile.Services;

namespace VK.Mobile.Platforms.Android;

/// <summary>
/// Android Foreground Service để GPS tracking hoạt động khi app ở background.
/// Hiển thị persistent notification, tránh bị OS kill.
/// </summary>
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    private const string ChannelId = "vk_location_channel";
    private const int NotificationId = 1001;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;

    public static bool IsRunning { get; private set; }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP")
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            IsRunning = false;
            return StartCommandResult.NotSticky;
        }

        CreateNotificationChannel();
        var notification = BuildNotification();
        StartForeground(NotificationId, notification,
            global::Android.Content.PM.ForegroundService.TypeLocation);
        IsRunning = true;

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        IsRunning = false;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            L["AndroidLocationChannelName"],
            NotificationImportance.Low)
        {
            Description = L["AndroidLocationChannelDescription"]
        };

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var intent = global::Android.App.Application.Context.PackageManager?
            .GetLaunchIntentForPackage(global::Android.App.Application.Context.PackageName!);
        var pendingIntent = PendingIntent.GetActivity(
            this, 0, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(L["AppName"])
            .SetContentText(L["AndroidLocationNotificationText"])
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogMap)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .Build();
    }

    // --- Static helpers to start/stop from app code ---

    public static void Start(Context context)
    {
        var intent = new Intent(context, typeof(LocationForegroundService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void Stop(Context context)
    {
        var intent = new Intent(context, typeof(LocationForegroundService));
        intent.SetAction("STOP");
        context.StartService(intent);
    }
}
