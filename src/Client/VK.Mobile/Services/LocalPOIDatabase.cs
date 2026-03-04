using SQLite;
using System.Text.Json;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

/// <summary>
/// SQLite cache cho danh sách POI — cho phép app hoạt động offline.
/// Dữ liệu được lưu dưới dạng JSON blob trong bảng poi_cache.
/// </summary>
public class LocalPOIDatabase
{
    private static readonly string DbPath =
        Path.Combine(FileSystem.AppDataDirectory, "poi_cache.db");

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db == null)
        {
            _db = new SQLiteAsyncConnection(DbPath);
            await _db.CreateTableAsync<PoiCacheEntry>();
        }
        return _db;
    }

    /// <summary>Lưu toàn bộ danh sách POI vào SQLite.</summary>
    public async Task SavePOIsAsync(List<POIModel> pois)
    {
        try
        {
            var db = await GetDbAsync();
            var entry = new PoiCacheEntry
            {
                Id = 1,
                JsonData = JsonSerializer.Serialize(pois, _json),
                CachedAt = DateTime.UtcNow
            };
            await db.InsertOrReplaceAsync(entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] SavePOIs error: {ex.Message}");
        }
    }

    /// <summary>
    /// Đọc POI từ cache SQLite.
    /// Trả về list rỗng nếu chưa có cache hoặc lỗi.
    /// </summary>
    public async Task<List<POIModel>> GetCachedPOIsAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var entry = await db.FindAsync<PoiCacheEntry>(1);
            if (entry == null || string.IsNullOrEmpty(entry.JsonData))
                return new List<POIModel>();

            return JsonSerializer.Deserialize<List<POIModel>>(entry.JsonData, _json)
                   ?? new List<POIModel>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] GetCachedPOIs error: {ex.Message}");
            return new List<POIModel>();
        }
    }

    /// <summary>Thời điểm cache cuối (null nếu chưa có cache).</summary>
    public async Task<DateTime?> GetCacheAgeAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var entry = await db.FindAsync<PoiCacheEntry>(1);
            return entry?.CachedAt;
        }
        catch { return null; }
    }

    /// <summary>Xóa toàn bộ cache.</summary>
    public async Task ClearAsync()
    {
        try
        {
            var db = await GetDbAsync();
            await db.DeleteAllAsync<PoiCacheEntry>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] Clear error: {ex.Message}");
        }
    }
}

[SQLite.Table("poi_cache")]
public class PoiCacheEntry
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public string JsonData { get; set; } = string.Empty;

    public DateTime CachedAt { get; set; }
}
