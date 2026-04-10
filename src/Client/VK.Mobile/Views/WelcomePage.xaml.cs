using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await App.TryOpenPendingPaymentAsync();
    }
}
