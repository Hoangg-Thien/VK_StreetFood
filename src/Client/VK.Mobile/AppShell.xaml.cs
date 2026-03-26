using VK.Mobile.Services;
using VK.Mobile.Views;

namespace VK.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute("POIDetail", typeof(POIDetailPage));
		Routing.RegisterRoute("poidetail", typeof(POIDetailPage));
		Routing.RegisterRoute("NowPlaying", typeof(NowPlayingPage));
		Routing.RegisterRoute("FavoritesPage", typeof(FavoritesPage));

		// Lắng nghe thay đổi ngôn ngữ để cập nhật tab titles
		LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateTabTitles();
		UpdateTabTitles();
	}

	private void UpdateTabTitles()
	{
		var L = LocalizationResourceManager.Instance;
		TabMap.Title = L["TabMap"];
		TabMenu.Title = L["TabExplore"];
		TabTours.Title = L["TabTours"];
		TabProfile.Title = L["TabProfile"];
		TabSettings.Title = L["TabSettings"];
	}
}
