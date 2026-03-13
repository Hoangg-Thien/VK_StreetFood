using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class MenuPage : ContentPage
{
    private readonly MenuViewModel _viewModel;

    public MenuPage(MenuViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.IsLoading)
            return;

        if (!_viewModel.FilteredPois.Any() || !string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
            await _viewModel.LoadPOIsCommand.ExecuteAsync(null);
    }
}
