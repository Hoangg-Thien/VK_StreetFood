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
    private string _statusMessage = "Vui lòng xác nhận thanh toán để tiếp tục.";

    [ObservableProperty]
    private decimal _amountVnd;

    [ObservableProperty]
    private int _qrTtlMinutes = 15;

    [ObservableProperty]
    private string _deepLinkName = "pay";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    private bool _isQrExpired;

    public bool HasPoiContext => PoiId > 0;

    public string AmountText => string.Format(
        System.Globalization.CultureInfo.GetCultureInfo("vi-VN"),
        "{0:N0} VND",
        AmountVnd);

    public bool CanPay => !IsProcessing && !IsPaid && !IsQrExpired;

    partial void OnAmountVndChanged(decimal value) => OnPropertyChanged(nameof(AmountText));
    partial void OnPoiIdChanged(int value) => OnPropertyChanged(nameof(HasPoiContext));

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
            ? "Nhấn Thanh toán để bắt đầu khám phá"
            : "Nhấn Thanh toán để mở khóa vào app.";

        _ = LoadQrPaymentConfigAsync();
    }

    private async Task LoadQrPaymentConfigAsync()
    {
        var config = await _apiService.GetQrPaymentConfigAsync();
        AmountVnd = config?.DefaultAmountVnd ?? 0;
        QrTtlMinutes = config?.QrTtlMinutes is > 0 ? config.QrTtlMinutes : 15;
        DeepLinkName = string.IsNullOrWhiteSpace(config?.DeepLinkName) ? "pay" : config.DeepLinkName.Trim().ToLowerInvariant();

        ValidateQrExpiry();
    }

    private void ValidateQrExpiry()
    {
        var issuedAt = App.PendingQrIssuedAtUtc;
        if (!issuedAt.HasValue)
        {
            IsQrExpired = false;
            return;
        }

        var expiresAt = issuedAt.Value.AddMinutes(QrTtlMinutes);
        IsQrExpired = DateTimeOffset.UtcNow > expiresAt;

        if (IsQrExpired)
        {
            StatusMessage = "QR đã hết hạn. Vui lòng quét lại mã mới.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task PayAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Đang xử lý thanh toán...";

            await Task.Delay(1200);

            ValidateQrExpiry();
            if (IsQrExpired)
            {
                return;
            }

            var touristId = await EnsureTouristIdAsync();
            var location = await _locationService.GetCurrentLocationAsync();
            var analyticsPoiId = await ResolveAnalyticsPoiIdAsync(location?.Latitude, location?.Longitude);

            if (touristId.HasValue && analyticsPoiId.HasValue)
            {
                if (PoiId > 0)
                {
                    var visitLogged = await _apiService.LogVisitAsync(
                        touristId.Value,
                        PoiId,
                        "qr_payment",
                        location?.Latitude,
                        location?.Longitude);

                    if (!visitLogged)
                    {
                        _logger.LogWarning(
                            "Payment log visit failed for tourist {TouristId}, poi {PoiId}",
                            touristId.Value,
                            PoiId);
                    }
                }

                var language = LocalizationResourceManager.Instance.CurrentLanguage;

                var paymentTracked = await _apiService.TrackEventAsync(
                    touristId.Value,
                    analyticsPoiId.Value,
                    "qr_payment",
                    language);
                if (!paymentTracked)
                {
                    _logger.LogWarning(
                        "Payment analytics event qr_payment failed for tourist {TouristId}, poi {PoiId}",
                        touristId.Value,
                        analyticsPoiId.Value);
                }

                var paymentSuccessTracked = await _apiService.TrackEventAsync(
                    touristId.Value,
                    analyticsPoiId.Value,
                    "qr_payment_success",
                    language);
                if (!paymentSuccessTracked)
                {
                    _logger.LogWarning(
                        "Payment analytics event qr_payment_success failed for tourist {TouristId}, poi {PoiId}",
                        touristId.Value,
                        analyticsPoiId.Value);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Skip payment analytics: TouristId={TouristId}, PoiId={PoiId}, ResolvedAnalyticsPoiId={ResolvedPoiId}",
                    touristId,
                    PoiId,
                    analyticsPoiId);
            }

            IsPaid = true;
            App.MarkPendingPaymentCompleted();
            StatusMessage = "Thanh toán thành công. Bạn đã được mở khóa vào app.";

            await Shell.Current.DisplayAlert("Thanh toán", "Thanh toán thành công", "OK");
            await Shell.Current.GoToAsync("//Welcome");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment flow failed for POI {PoiId}", PoiId);
            StatusMessage = "Thanh toán thất bại. Vui lòng thử lại.";
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
            var cachedTouristId = await _storageService.GetTouristIdAsync();
            if (cachedTouristId.HasValue)
            {
                _logger.LogWarning(
                    "RegisterTourist failed during payment; falling back to cached touristId {TouristId}",
                    cachedTouristId.Value);
                return cachedTouristId.Value;
            }

            return null;
        }

        await _storageService.SetTouristIdAsync(tourist.Id);
        await _storageService.SetTouristAsync(tourist);
        return tourist.Id;
    }

    private async Task<int?> ResolveAnalyticsPoiIdAsync(double? latitude, double? longitude)
    {
        if (PoiId > 0)
        {
            return PoiId;
        }

        if (App.PendingPoiId is int pendingPoiId && pendingPoiId > 0)
        {
            return pendingPoiId;
        }

        var language = LocalizationResourceManager.Instance.CurrentLanguage;
        if (string.IsNullOrWhiteSpace(language))
        {
            language = "vi";
        }

        try
        {
            if (latitude.HasValue && longitude.HasValue)
            {
                var nearby = await _apiService.GetNearbyPOIsAsync(
                    latitude.Value,
                    longitude.Value,
                    2.0,
                    language);

                var nearestPoi = nearby
                    .OrderBy(p => p.DistanceKm ?? double.MaxValue)
                    .FirstOrDefault();

                if (nearestPoi?.Id > 0)
                {
                    return nearestPoi.Id;
                }
            }

            var allPois = await _apiService.GetAllPOIsAsync(languageCode: language);
            return allPois.FirstOrDefault(p => p.Id > 0)?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve fallback POI for payment analytics");
            return null;
        }
    }
}
