using VK.Mobile.Services;

namespace VK.Mobile;

public partial class App : Application
{
	private const string PaymentUnlockedKey = "payment_unlocked";
	private static int? _pendingPoiId;
	private static bool _pendingPaymentRequired;
	private static bool _pendingPaymentCompleted;
	private static DateTimeOffset? _pendingQrIssuedAtUtc;

	public static bool IsPaymentUnlocked
		=> Preferences.Default.Get(PaymentUnlockedKey, false);

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	protected override void OnAppLinkRequestReceived(Uri uri)
	{
		base.OnAppLinkRequestReceived(uri);
		TryCapturePendingFromUri(uri);
		_ = TryOpenPendingPaymentAsync();
	}

	public static bool TryCapturePendingFromUri(Uri? uri)
	{
		if (!TryResolvePaymentDeepLink(uri, out var poiId))
		{
			return false;
		}

		if (IsPaymentUnlocked)
		{
			_pendingPoiId = poiId;
			_pendingPaymentRequired = false;
			_pendingPaymentCompleted = true;
			_pendingQrIssuedAtUtc = null;
			return true;
		}

		_pendingPoiId = poiId;
		_pendingPaymentRequired = true;
		_pendingPaymentCompleted = false;
		_pendingQrIssuedAtUtc = ResolveQrIssuedAt(uri) ?? DateTimeOffset.UtcNow;
		return true;
	}

	public static bool HasPendingPayment
		=> _pendingPaymentRequired && !_pendingPaymentCompleted;

	public static int? PendingPoiId => _pendingPoiId;
	public static DateTimeOffset? PendingQrIssuedAtUtc => _pendingQrIssuedAtUtc;

	public static void MarkPendingPaymentCompleted()
	{
		Preferences.Default.Set(PaymentUnlockedKey, true);
		_pendingPaymentCompleted = true;
		_pendingPaymentRequired = false;
		_pendingPoiId = null;
		_pendingQrIssuedAtUtc = null;
	}

	public static Task<bool> TryOpenPendingPaymentAsync()
	{
		if (!HasPendingPayment || Shell.Current == null)
		{
			return Task.FromResult(false);
		}

		return MainThread.InvokeOnMainThreadAsync(async () =>
		{
			try
			{
				var currentRoute = Shell.Current.CurrentState?.Location?.OriginalString ?? string.Empty;
				if (currentRoute.Contains("Payment", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}

				var query = new Dictionary<string, object>
				{
					["fromQr"] = "1"
				};

				if (_pendingPoiId is int pendingPoiId && pendingPoiId > 0)
				{
					query["poiId"] = pendingPoiId;
				}

				await Shell.Current.GoToAsync("Payment", query);
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[DeepLink] Open Payment failed: {ex.Message}");
				return false;
			}
		});
	}

	private static bool TryResolvePaymentDeepLink(Uri? uri, out int? poiId)
	{
		poiId = null;

		if (uri == null)
		{
			return false;
		}

		if (!uri.Scheme.Equals("vkstreetfood", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var trimmed = uri.AbsolutePath.Trim('/');
		if (uri.Host.Equals("pay", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(trimmed, out var payPoiId) && payPoiId > 0)
			{
				poiId = payPoiId;
			}

			var payQueryPoiId = GetQueryValue(uri.Query, "poiId") ?? GetQueryValue(uri.Query, "id");
			if (int.TryParse(payQueryPoiId, out var payQueryParsedPoiId) && payQueryParsedPoiId > 0)
			{
				poiId = payQueryParsedPoiId;
			}

			return true;
		}

		if (uri.Host.Equals(".", StringComparison.OrdinalIgnoreCase))
		{
			var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
			if (segments.Length == 0 || !segments[0].Equals("pay", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (segments.Length >= 2 && int.TryParse(segments[1], out var legacyPoiId) && legacyPoiId > 0)
			{
				poiId = legacyPoiId;
			}

			var legacyQueryPoiId = GetQueryValue(uri.Query, "poiId") ?? GetQueryValue(uri.Query, "id");
			if (int.TryParse(legacyQueryPoiId, out var legacyQueryParsedPoiId) && legacyQueryParsedPoiId > 0)
			{
				poiId = legacyQueryParsedPoiId;
			}

			return true;
		}

		if (!uri.Host.Equals("poi", StringComparison.OrdinalIgnoreCase)
			&& !uri.Host.Equals("open", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (int.TryParse(trimmed, out var parsedPoiId) && parsedPoiId > 0)
		{
			poiId = parsedPoiId;
			return true;
		}

		var poiIdRaw = GetQueryValue(uri.Query, "poiId") ?? GetQueryValue(uri.Query, "id");
		if (int.TryParse(poiIdRaw, out var queryPoiId) && queryPoiId > 0)
		{
			poiId = queryPoiId;
			return true;
		}

		return false;
	}

	private static DateTimeOffset? ResolveQrIssuedAt(Uri? uri)
	{
		if (uri == null)
		{
			return null;
		}

		var unixRaw = GetQueryValue(uri.Query, "ts")
			?? GetQueryValue(uri.Query, "iat")
			?? GetQueryValue(uri.Query, "issuedAt");

		if (!long.TryParse(unixRaw, out var unixSeconds) || unixSeconds <= 0)
		{
			return null;
		}

		try
		{
			return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
		}
		catch
		{
			return null;
		}
	}

	private static string? GetQueryValue(string query, string key)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return null;
		}

		var q = query.TrimStart('?');
		var pairs = q.Split('&', StringSplitOptions.RemoveEmptyEntries);
		foreach (var pair in pairs)
		{
			var kv = pair.Split('=', 2);
			if (kv.Length == 0)
			{
				continue;
			}

			if (kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
			}
		}

		return null;
	}
}