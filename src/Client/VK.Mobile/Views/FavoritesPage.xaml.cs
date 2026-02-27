using VK.Mobile.Services;
using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class FavoritesPage : ContentPage
{
    private readonly FavoritesViewModel _viewModel;

    public FavoritesPage(FavoritesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateTitle();
        LocalizationResourceManager.Instance.PropertyChanged += OnLanguageChanged;
        await _viewModel.LoadFavoritesAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationResourceManager.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => UpdateTitle();

    private void UpdateTitle()
        => Title = LocalizationResourceManager.Instance["TabFavorites"];
}

