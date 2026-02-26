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
}
