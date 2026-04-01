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
            await _db.CreateTableAsync<AudioScriptCacheEntry>();
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

            var list = JsonSerializer.Deserialize<List<POIModel>>(entry.JsonData, _json)
                       ?? new List<POIModel>();

            if (list.Count == 0)
                return new List<POIModel>();

            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] GetCachedPOIs error: {ex.Message}");
            return new List<POIModel>();
        }
    }

    /// <summary>Số lượng POI hiện có trong cache.</summary>
    public async Task<int> GetCachedPoiCountAsync()
    {
        var list = await GetCachedPOIsAsync();
        return list.Count;
    }

    /// <summary>Lưu script thuyết minh theo từng POI + ngôn ngữ.</summary>
    public async Task SaveAudioScriptAsync(
        int poiId,
        string languageCode,
        string textContent,
        string? audioFileUrl = null,
        int? durationInSeconds = null,
        string? localAudioPath = null)
    {
        if (string.IsNullOrWhiteSpace(textContent)) return;

        try
        {
            var db = await GetDbAsync();
            var lang = string.IsNullOrWhiteSpace(languageCode)
                ? "vi"
                : languageCode.Trim().ToLowerInvariant();

            var existing = await db.Table<AudioScriptCacheEntry>()
                .FirstOrDefaultAsync(x => x.PoiId == poiId && x.LanguageCode == lang);

            if (existing == null)
            {
                await db.InsertAsync(new AudioScriptCacheEntry
                {
                    PoiId = poiId,
                    LanguageCode = lang,
                    TextContent = textContent,
                    AudioFileUrl = audioFileUrl,
                    DurationInSeconds = durationInSeconds,
                    LocalAudioPath = localAudioPath,
                    CachedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.TextContent = textContent;
                existing.AudioFileUrl = audioFileUrl;
                existing.DurationInSeconds = durationInSeconds;
                existing.LocalAudioPath = localAudioPath;
                existing.CachedAt = DateTime.UtcNow;
                await db.UpdateAsync(existing);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] SaveAudioScript error: {ex.Message}");
        }
    }

    public async Task<AudioScriptCacheEntry?> GetAudioScriptAsync(int poiId, string languageCode)
    {
        try
        {
            var db = await GetDbAsync();
            var lang = string.IsNullOrWhiteSpace(languageCode)
                ? "vi"
                : languageCode.Trim().ToLowerInvariant();

            return await db.Table<AudioScriptCacheEntry>()
                .FirstOrDefaultAsync(x => x.PoiId == poiId && x.LanguageCode == lang);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] GetAudioScript error: {ex.Message}");
            return null;
        }
    }

    /// <summary>Lấy text script từ cache, fallback sang tiếng Việt nếu thiếu ngôn ngữ hiện tại.</summary>
    public async Task<string?> GetCachedNarrationTextAsync(int poiId, string languageCode)
    {
        var script = await GetAudioScriptAsync(poiId, languageCode);
        if (!string.IsNullOrWhiteSpace(script?.TextContent))
            return script!.TextContent;

        if (!string.Equals(languageCode, "vi", StringComparison.OrdinalIgnoreCase))
        {
            var viScript = await GetAudioScriptAsync(poiId, "vi");
            if (!string.IsNullOrWhiteSpace(viScript?.TextContent))
                return viScript!.TextContent;
        }

        return null;
    }

    public async Task<int> GetAudioScriptCountAsync()
    {
        try
        {
            var db = await GetDbAsync();
            return await db.Table<AudioScriptCacheEntry>().CountAsync();
        }
        catch
        {
            return 0;
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
            await db.DeleteAllAsync<AudioScriptCacheEntry>();
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

[SQLite.Table("audio_script_cache")]
public class AudioScriptCacheEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_AudioScript_PoiLang", Order = 1, Unique = true)]
    public int PoiId { get; set; }

    [Indexed(Name = "IX_AudioScript_PoiLang", Order = 2, Unique = true)]
    public string LanguageCode { get; set; } = "vi";

    public string TextContent { get; set; } = string.Empty;

    public string? AudioFileUrl { get; set; }

    public int? DurationInSeconds { get; set; }

    public string? LocalAudioPath { get; set; }

    public DateTime CachedAt { get; set; }
}
