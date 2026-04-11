using VK.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace VK.Mobile.Views;

public partial class PaymentPage : ContentPage
{
    public PaymentPage()
        : this(ResolveViewModel())
    {
    }

    public PaymentPage(PaymentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private static PaymentViewModel ResolveViewModel()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services == null)
        {
            throw new InvalidOperationException("Service provider is not available for PaymentPage.");
        }

        return services.GetRequiredService<PaymentViewModel>();
    }
}
