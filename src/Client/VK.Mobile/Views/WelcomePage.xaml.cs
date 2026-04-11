using VK.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace VK.Mobile.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
        : this(ResolveViewModel())
    {
    }

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

    private static WelcomeViewModel ResolveViewModel()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        return services?.GetService<WelcomeViewModel>() ?? new WelcomeViewModel();
    }
}
