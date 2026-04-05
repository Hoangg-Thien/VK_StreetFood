using VK.Mobile.Services;

namespace VK.Mobile;

public partial class App : Application
{
	private static int? _pendingPoiId;
	private static bool _pendingAutoPlay;
	private static readonly SemaphoreSlim _pendingNavigationGate = new(1, 1);

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
		_ = TryOpenPendingNarrationAsync();
	}

	public static bool TryCapturePendingFromUri(Uri? uri)
	{
		if (!TryResolvePoiDeepLink(uri, out var poiId, out var autoplay))
		{
			return false;
		}

		_pendingPoiId = poiId;
		_pendingAutoPlay = autoplay;
		return true;
	}

	public static Task<bool> TryOpenPendingNarrationAsync()
	{
		return TryOpenPendingNarrationAsync(forceFromWelcome: false);
	}

	public static async Task<bool> TryOpenPendingNarrationAsync(bool forceFromWelcome)
	{
		if (_pendingPoiId is not int poiId || poiId <= 0)
		{
			return false;
		}

		if (Shell.Current == null)
		{
			return false;
		}

		if (!forceFromWelcome && IsOnWelcomeRoute())
		{
			return false;
		}

		await _pendingNavigationGate.WaitAsync();

		try
		{
			if (_pendingPoiId is not int pendingPoiId || pendingPoiId <= 0)
			{
				return false;
			}

			var language = LocalizationResourceManager.Instance.CurrentLanguage;
			if (string.IsNullOrWhiteSpace(language))
			{
				language = "vi";
			}

			var query = new Dictionary<string, object>
			{
				["poiId"] = pendingPoiId,
				["autoplay"] = _pendingAutoPlay ? "1" : "0",
				["language"] = language,
				["fromQr"] = "1"
			};

			var opened = false;
			for (var attempt = 0; attempt < 3 && !opened; attempt++)
			{
				try
				{
					await MainThread.InvokeOnMainThreadAsync(async () =>
					{
						await Shell.Current.GoToAsync("NowPlaying", query);
					});
					opened = true;
				}
				catch (Exception ex) when (attempt < 2)
				{
					System.Diagnostics.Debug.WriteLine($"[DeepLink] Retry open NowPlaying ({attempt + 1}/3): {ex.Message}");
					await Task.Delay(150);
				}
			}

			if (!opened)
			{
				return false;
			}

			_pendingPoiId = null;
			_pendingAutoPlay = false;
			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[DeepLink] Open NowPlaying failed: {ex.Message}");
			return false;
		}
		finally
		{
			_pendingNavigationGate.Release();
		}
	}

	private static bool IsOnWelcomeRoute()
	{
		var location = Shell.Current?.CurrentState?.Location?.OriginalString;
		if (string.IsNullOrWhiteSpace(location))
		{
			return false;
		}

		return location.Contains("Welcome", StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryResolvePoiDeepLink(Uri? uri, out int poiId, out bool autoplay)
	{
		poiId = 0;
		autoplay = false;

		if (uri == null)
		{
			return false;
		}

		if (!uri.Scheme.Equals("vkstreetfood", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!uri.Host.Equals("poi", StringComparison.OrdinalIgnoreCase)
			&& !uri.Host.Equals("open", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var trimmed = uri.AbsolutePath.Trim('/');
		if (!int.TryParse(trimmed, out poiId) || poiId <= 0)
		{
			var poiIdRaw = GetQueryValue(uri.Query, "poiId") ?? GetQueryValue(uri.Query, "id");
			if (!int.TryParse(poiIdRaw, out poiId) || poiId <= 0)
			{
				return false;
			}
		}

		autoplay = GetQueryValue(uri.Query, "autoplay") is "1" or "true";
		return true;
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