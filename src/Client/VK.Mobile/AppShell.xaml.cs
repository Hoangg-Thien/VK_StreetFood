using VK.Mobile.Views;

namespace VK.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute("poidetail", typeof(POIDetailPage));
		Routing.RegisterRoute("qrscan", typeof(QRScanPage));
	}
}
