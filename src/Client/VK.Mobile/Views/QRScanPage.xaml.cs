using VK.Mobile.ViewModels;
using ZXing.Net.Maui;

namespace VK.Mobile.Views;

public partial class QRScanPage : ContentPage
{
    private readonly QRScanViewModel _viewModel;

    public QRScanPage(QRScanViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Configure barcode reader
        BarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional | BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results?.Length > 0 && !_viewModel.IsProcessing)
        {
            var barcode = e.Results[0];
            await _viewModel.QRCodeDetectedCommand.ExecuteAsync(barcode.Value);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BarcodeReaderView.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReaderView.IsDetecting = false;
    }
}
