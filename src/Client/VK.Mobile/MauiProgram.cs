using Microsoft.Extensions.Logging;
using VK.Mobile.Services;
using VK.Mobile.ViewModels;
using VK.Mobile.Views;
using Plugin.Maui.Audio;
using ZXing.Net.Maui;
using CommunityToolkit.Maui;
using ZXing.Net.Maui.Controls;

namespace VK.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
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
		builder.Services.AddSingleton<ILocationService, LocationService>();
		builder.Services.AddSingleton<IAudioService, AudioService>();
		builder.Services.AddSingleton<ITTSService, TTSService>();
		builder.Services.AddSingleton<StorageService>();
		builder.Services.AddSingleton(AudioManager.Current);

		// Register HttpClient
		builder.Services.AddSingleton<HttpClient>();

		// Register ViewModels
		builder.Services.AddTransient<WelcomeViewModel>();
		builder.Services.AddTransient<MainMapViewModel>();
		builder.Services.AddTransient<POIDetailViewModel>();
		builder.Services.AddTransient<QRScanViewModel>();
		builder.Services.AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<HistoryViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();
		builder.Services.AddTransient<AnalyticsViewModel>();

		// Register Views
		builder.Services.AddTransient<WelcomePage>();
		builder.Services.AddTransient<MainMapPage>();
		builder.Services.AddTransient<POIDetailPage>();
		builder.Services.AddTransient<QRScanPage>();
		builder.Services.AddTransient<FavoritesPage>();
		builder.Services.AddTransient<HistoryPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<AnalyticsPage>();

		return builder.Build();
	}
}
