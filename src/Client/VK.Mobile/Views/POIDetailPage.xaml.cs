using VK.Mobile.ViewModels;

namespace VK.Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    private readonly POIDetailViewModel _viewModel;

    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private void OnSliderDragStarted(object sender, EventArgs e)
    {
        _viewModel.IsDragging = true;
    }

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        _viewModel.IsDragging = false;
        _ = _viewModel.SeekAudioCommand.ExecuteAsync(((Slider)sender).Value);
    }
}
