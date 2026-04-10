using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class PaymentViewModel : ObservableObject, IQueryAttributable
{
    private readonly IApiService _apiService;
    private readonly StorageService _storageService;
    private readonly ILocationService _locationService;
    private readonly ILogger<PaymentViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    private int _poiId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    private bool _isPaid;

    [ObservableProperty]
    private string _statusMessage = "Vui long xac nhan thanh toan de tiep tuc.";

    public string AmountText => "20.000 VND";

    public bool CanPay => PoiId > 0 && !IsProcessing && !IsPaid;

    public PaymentViewModel(
        IApiService apiService,
        StorageService storageService,
        ILocationService locationService,
        ILogger<PaymentViewModel> logger)
    {
        _apiService = apiService;
        _storageService = storageService;
        _locationService = locationService;
        _logger = logger;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("poiId", out var poiVal))
        {
            PoiId = poiVal is int i
                ? i
                : int.TryParse(poiVal?.ToString(), out var parsed) ? parsed : 0;
        }
        else if (App.PendingPoiId is int pendingPoiId)
        {
            PoiId = pendingPoiId;
        }

        StatusMessage = PoiId > 0
            ? "Nhan Thanh toan de mo khoa va bat dau tu Welcome."
            : "Khong tim thay POI tu QR. Vui long quet lai ma QR.";
    }

    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task PayAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Dang xu ly thanh toan...";

            await Task.Delay(1200);

            var touristId = await EnsureTouristIdAsync();

            if (touristId.HasValue && PoiId > 0)
            {
                var location = await _locationService.GetCurrentLocationAsync();

                await _apiService.LogVisitAsync(
                    touristId.Value,
                    PoiId,
                    "qr_payment",
                    location?.Latitude,
                    location?.Longitude);

                await _apiService.TrackEventAsync(
                    touristId.Value,
                    PoiId,
                    "qr_payment_success",
                    LocalizationResourceManager.Instance.CurrentLanguage);
            }

            IsPaid = true;
            App.MarkPendingPaymentCompleted();
            StatusMessage = "Thanh toan thanh cong. Ban da duoc mo khoa vao app.";

            await Shell.Current.DisplayAlert("Thanh toan", "Thanh toan thanh cong", "OK");
            await Shell.Current.GoToAsync("//Welcome");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment flow failed for POI {PoiId}", PoiId);
            StatusMessage = "Thanh toan that bai. Vui long thu lai.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private Task BackToWelcomeAsync()
        => Shell.Current.GoToAsync("//Welcome");

    private async Task<int?> EnsureTouristIdAsync()
    {
        var touristId = await _storageService.GetTouristIdAsync();
        if (touristId.HasValue)
        {
            return touristId;
        }

        var deviceId = await _storageService.GetDeviceIdAsync();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString();
            await _storageService.SetDeviceIdAsync(deviceId);
        }

        var language = LocalizationResourceManager.Instance.CurrentLanguage;
        if (string.IsNullOrWhiteSpace(language))
        {
            language = "vi";
        }

        var location = await _locationService.GetCurrentLocationAsync();
        var tourist = await _apiService.RegisterTouristAsync(
            deviceId,
            language,
            location?.Latitude,
            location?.Longitude);

        if (tourist == null)
        {
            return null;
        }

        await _storageService.SetTouristIdAsync(tourist.Id);
        await _storageService.SetTouristAsync(tourist);
        return tourist.Id;
    }
}
