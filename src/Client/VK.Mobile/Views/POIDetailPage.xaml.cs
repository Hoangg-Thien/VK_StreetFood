using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
