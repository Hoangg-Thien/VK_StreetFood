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
		Routing.RegisterRoute("QRScan", typeof(QRScanPage));
		Routing.RegisterRoute("qrscan", typeof(QRScanPage));
		Routing.RegisterRoute("analytics", typeof(AnalyticsPage));

		// Lắng nghe thay đổi ngôn ngữ để cập nhật tab titles
		LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateTabTitles();
	}

	private void UpdateTabTitles()
	{
		var L = LocalizationResourceManager.Instance;
		TabMap.Title = L["TabMap"];
		TabFavorites.Title = L["TabFavorites"];
		TabHistory.Title = L["TabHistory"];
		TabProfile.Title = L["TabProfile"];
		TabSettings.Title = L["TabSettings"];
	}
}
