using System.Text.Json;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface IStorageService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
    Task<bool> RemoveAsync(string key);
    Task ClearAsync();
}

public class StorageService : IStorageService
{
    private const string TouristIdKey = "tourist_id";
    private const string DeviceIdKey = "device_id";
    private const string PreferredLanguageKey = "preferred_language";

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);

            if (string.IsNullOrEmpty(value))
                return default;

            if (typeof(T) == typeof(string))
                return (T)(object)value;

            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(value);

            return default;
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        if (value == null)
            return;

        await SecureStorage.SetAsync(key, value.ToString() ?? string.Empty);
    }

    public Task<bool> RemoveAsync(string key)
    {
        SecureStorage.Remove(key);
        return Task.FromResult(true);
    }

    public Task ClearAsync()
    {
        SecureStorage.RemoveAll();
        return Task.CompletedTask;
    }

    // Helper methods
    public async Task<int?> GetTouristIdAsync()
    {
        var result = await GetAsync<int>(TouristIdKey);
        return result == 0 ? null : result;
    }
    public Task SetTouristIdAsync(int id) => SetAsync(TouristIdKey, id);

    public Task<string?> GetDeviceIdAsync() => GetAsync<string>(DeviceIdKey);
    public Task SetDeviceIdAsync(string id) => SetAsync(DeviceIdKey, id);

    public Task<string?> GetPreferredLanguageAsync() => GetAsync<string>(PreferredLanguageKey);
    public Task SetPreferredLanguageAsync(string lang) => SetAsync(PreferredLanguageKey, lang);

    // Tourist model storage
    private const string TouristKey = "tourist";

    public async Task<TouristModel?> GetTouristAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(TouristKey);
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<TouristModel>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTouristAsync(TouristModel tourist)
    {
        var json = JsonSerializer.Serialize(tourist);
        await SecureStorage.SetAsync(TouristKey, json);
    }

    // ── Lịch sử vị trí ẩn danh (dùng cho heatmap) ─────────────────────────────
    private const string LocationHistoryKey = "location_history";
    private const int MaxLocationHistory = 2000;

    /// <summary>Lưu một điểm vị trí ẩn danh vào lịch sử.</summary>
    public void AppendLocation(double latitude, double longitude)
    {
        try
        {
            var json = Preferences.Default.Get(LocationHistoryKey, "[]");
            var list = JsonSerializer.Deserialize<List<HeatmapPoint>>(json) ?? new();
            list.Add(new HeatmapPoint(latitude, longitude));
            if (list.Count > MaxLocationHistory)
                list = list.GetRange(list.Count - MaxLocationHistory, MaxLocationHistory);
            Preferences.Default.Set(LocationHistoryKey, JsonSerializer.Serialize(list));
        }
        catch { /* best effort – không̀ làm gián đoạn app */ }
    }

    /// <summary>Lấy toàn bộ lịch sử vị trí.</summary>
    public List<HeatmapPoint> GetLocationHistory()
    {
        try
        {
            var json = Preferences.Default.Get(LocationHistoryKey, "[]");
            return JsonSerializer.Deserialize<List<HeatmapPoint>>(json) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>Xóa toàn bộ lịch sử vị trí.</summary>
    public void ClearLocationHistory()
        => Preferences.Default.Remove(LocationHistoryKey);
}
