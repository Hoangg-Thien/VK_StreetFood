using VK.Mobile.Services;
using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateTitle();
        LocalizationResourceManager.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationResourceManager.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => UpdateTitle();

    private void UpdateTitle()
        => Title = LocalizationResourceManager.Instance["TabSettings"];
}

