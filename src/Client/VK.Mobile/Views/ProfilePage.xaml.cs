using VK.Mobile.Services;
using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateTitle();
        LocalizationResourceManager.Instance.PropertyChanged += OnLanguageChanged;
        await _viewModel.LoadProfileAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationResourceManager.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => UpdateTitle();

    private void UpdateTitle()
        => Title = LocalizationResourceManager.Instance["TabProfile"];
}

