using Microsoft.Extensions.Logging;
using VK.Mobile.Services;
using VK.Mobile.ViewModels;
using VK.Mobile.Views;
using Plugin.Maui.Audio;
using ZXing.Net.Maui;
using CommunityToolkit.Maui;
using ZXing.Net.Maui.Controls;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace VK.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.UseBarcodeReader()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register Services
		builder.Services.AddSingleton<IApiService, ApiService>();
		builder.Services.AddSingleton<ITouristApiClient, TouristApiClient>();
		builder.Services.AddSingleton<IPoiApiClient, PoiApiClient>();
		builder.Services.AddSingleton<IAudioApiClient, AudioApiClient>();
		builder.Services.AddSingleton<IAnalyticsApiClient, AnalyticsApiClient>();
		builder.Services.AddSingleton<ITourApiClient, TourApiClient>();
		builder.Services.AddSingleton<ILocalizationApiClient, LocalizationApiClient>();
		builder.Services.AddSingleton<ILocationService, LocationService>();
		builder.Services.AddSingleton<IRoutingService, OsrmRoutingService>();
		builder.Services.AddSingleton<IGeofenceEngine, GeofenceEngine>();
		builder.Services.AddSingleton<INarrationCoordinator, NarrationCoordinator>();
		builder.Services.AddSingleton<IAudioService, AudioService>();
		builder.Services.AddSingleton<IOfflineContentService, OfflineContentService>();
		builder.Services.AddSingleton<IOfflineSyncService>(sp => (OfflineContentService)sp.GetRequiredService<IOfflineContentService>());
		builder.Services.AddSingleton<IOfflineAudioDownloader, OfflineAudioDownloader>();
		builder.Services.AddSingleton<IMapTileCacheService, MapTileCacheService>();
		builder.Services.AddSingleton<IRoutePackageService, RoutePackageService>();
#if ANDROID
		builder.Services.AddSingleton<ITTSService, VK.Mobile.Platforms.Android.AndroidTTSService>();
#else
		builder.Services.AddSingleton<ITTSService, TTSService>();
#endif
		builder.Services.AddSingleton<StorageService>();
		builder.Services.AddSingleton<LocalPOIDatabase>();
		builder.Services.AddSingleton<ITourSessionService, TourSessionService>();
		builder.Services.AddSingleton(AudioManager.Current);

		// Register HttpClient
		builder.Services.AddSingleton<HttpClient>(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(8) });

		// Register ViewModels
		builder.Services.AddTransient<WelcomeViewModel>();
		builder.Services.AddTransient<MainMapViewModel>();
		builder.Services.AddTransient<POIDetailViewModel>();
		builder.Services.AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();
		builder.Services.AddTransient<MenuViewModel>();
		builder.Services.AddTransient<TourViewModel>();
		builder.Services.AddTransient<NowPlayingViewModel>();

		// Register Views
		builder.Services.AddTransient<WelcomePage>();
		builder.Services.AddTransient<MainMapPage>();
		builder.Services.AddTransient<POIDetailPage>();
		builder.Services.AddTransient<FavoritesPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<MenuPage>();
		builder.Services.AddTransient<TourPage>();
		builder.Services.AddTransient<NowPlayingPage>();

		return builder.Build();
	}
}
