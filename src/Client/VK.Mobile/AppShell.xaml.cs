using VK.Mobile.Views;

namespace VK.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute("POIDetail", typeof(POIDetailPage));
		Routing.RegisterRoute("QRScan", typeof(QRScanPage));
	}
}
