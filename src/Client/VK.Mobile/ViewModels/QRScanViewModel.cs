using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Models;
using VK.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.ViewModels;

public partial class QRScanViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILogger<QRScanViewModel> _logger;

    [ObservableProperty]
    private bool _isScanning = true;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = "Point camera at QR code";

    public QRScanViewModel(
        IApiService apiService,
        StorageService storageService,
        ILogger<QRScanViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task QRCodeDetectedAsync(string qrCode)
    {
        if (IsProcessing)
            return;

        try
        {
            IsProcessing = true;
            IsScanning = false;
            StatusMessage = "Processing QR code...";

            _logger.LogInformation("QR Code detected: {QRCode}", qrCode);

            var language = await _storageService.GetPreferredLanguageAsync() ?? "vi";
            var poi = await _apiService.ScanQRCodeAsync(qrCode, language);

            if (poi != null)
            {
                // Log visit
                var touristId = await _storageService.GetTouristIdAsync();
                if (touristId != null)
                {
                    await _apiService.LogVisitAsync(
                        touristId.Value,
                        poi.Id,
                        "qr_scan");

                    // Track analytics
                    await _apiService.TrackEventAsync(
                        touristId,
                        poi.Id,
                        "qr_scan",
                        language);
                }

                StatusMessage = "QR code scanned successfully!";

                // Navigate to POI detail
                var navigationParameter = new Dictionary<string, object>
                {
                    { "POI", poi }
                };

                await Shell.Current.GoToAsync("poidetail", navigationParameter);
            }
            else
            {
                StatusMessage = "Invalid QR code";
                await Shell.Current.DisplayAlert("Error", "QR code not found in system", "OK");

                // Resume scanning
                await Task.Delay(2000);
                IsScanning = true;
                StatusMessage = "Point camera at QR code";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing QR code");
            StatusMessage = "Error processing QR code";
            await Shell.Current.DisplayAlert("Error", "Failed to process QR code", "OK");

            // Resume scanning
            await Task.Delay(2000);
            IsScanning = true;
            StatusMessage = "Point camera at QR code";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
