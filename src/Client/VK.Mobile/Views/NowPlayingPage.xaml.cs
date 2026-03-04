using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class NowPlayingPage : ContentPage
{
    private readonly NowPlayingViewModel _viewModel;

    public NowPlayingPage(NowPlayingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        NowPlayingViewModel.AutoCloseRequested += OnAutoCloseRequested;
        if (!string.IsNullOrWhiteSpace(_viewModel.AudioText))
        {
            _viewModel.IsPlaying = true;
            await _viewModel.StartPlayingAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NowPlayingViewModel.AutoCloseRequested -= OnAutoCloseRequested;
    }

    private async void OnAutoCloseRequested(object? sender, EventArgs e)
    {
        // Tự đóng khi geofence khác trigger
        await _viewModel.CloseCommand.ExecuteAsync(null);
    }
}
