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
                return await GetBuiltInFallbackPoisAsync();

            var list = JsonSerializer.Deserialize<List<POIModel>>(entry.JsonData, _json)
                       ?? new List<POIModel>();

            if (list.Count == 0)
                return await GetBuiltInFallbackPoisAsync();

            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalPOIDatabase] GetCachedPOIs error: {ex.Message}");
            return await GetBuiltInFallbackPoisAsync();
        }
    }

    private async Task<List<POIModel>> GetBuiltInFallbackPoisAsync()
    {
        var fallback = BuildBuiltInFallbackPois();
        if (fallback.Count > 0)
        {
            try
            {
                await SavePOIsAsync(fallback);
            }
            catch
            {
            }
        }

        return fallback;
    }

    private static List<POIModel> BuildBuiltInFallbackPois() => new()
    {
        new POIModel
        {
            Id = 1,
            Name = "Cổng chào Phố Ẩm thực Vĩnh Khánh",
            Description = "Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh – thiên đường ẩm thực đêm của Sài Gòn.",
            Latitude = 10.7619058983358,
            Longitude = 106.702227165271,
            Address = "Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Landmark",
            ImageUrl = "/images/poi/cong-chao.jpg"
        },
        new POIModel
        {
            Id = 2,
            Name = "Ốc Vũ",
            Description = "Quán ốc lâu năm nổi tiếng với nước chấm sốt me đặc trưng.",
            Latitude = 10.7615184310278,
            Longitude = 106.7027154252,
            Address = "37 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/oc-vu.jpg"
        },
        new POIModel
        {
            Id = 3,
            Name = "Ốc Thảo",
            Description = "Quán ốc nổi tiếng với món ốc len xào dừa béo ngậy.",
            Latitude = 10.7617951625975,
            Longitude = 106.702392988972,
            Address = "383 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/oc-thao.jpg"
        },
        new POIModel
        {
            Id = 4,
            Name = "Ốc Sáu Nở",
            Description = "Quán ốc vỉa hè đậm chất Sài Gòn với món ốc hương trứng muối.",
            Latitude = 10.7610380785009,
            Longitude = 106.702904448097,
            Address = "128 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/oc-sau-no.jpg"
        },
        new POIModel
        {
            Id = 5,
            Name = "Ốc Oanh",
            Description = "Quán ốc nổi tiếng được Michelin Bib Gourmand.",
            Latitude = 10.7608486298266,
            Longitude = 106.703295774422,
            Address = "534 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/oc-oanh.jpg"
        },
        new POIModel
        {
            Id = 6,
            Name = "A Fat Hot Pot",
            Description = "Nhà hàng lẩu phong cách Hong Kong nổi tiếng với lẩu collagen.",
            Latitude = 10.7608069330753,
            Longitude = 106.703478752187,
            Address = "668 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Hotpot",
            ImageUrl = "/images/poi/a-fat.jpg"
        },
        new POIModel
        {
            Id = 7,
            Name = "Chilli Lẩu Nướng Tự Chọn",
            Description = "Buffet nướng ngoài trời rất được giới trẻ yêu thích.",
            Latitude = 10.7607944319756,
            Longitude = 106.703659068107,
            Address = "232/105 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Hotpot",
            ImageUrl = "/images/poi/chilli.jpg"
        },
        new POIModel
        {
            Id = 8,
            Name = "Alo Quán – Seafood & Beer",
            Description = "Quán hải sản hiện đại với không gian chill.",
            Latitude = 10.761127163188,
            Longitude = 106.704754254081,
            Address = "333 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/alo-quan.jpg"
        },
        new POIModel
        {
            Id = 9,
            Name = "Ốc Đào 2",
            Description = "Quán ốc nổi tiếng với khách du lịch quốc tế.",
            Latitude = 10.7613479651701,
            Longitude = 106.704967847399,
            Address = "232/123 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Seafood",
            ImageUrl = "/images/poi/oc-dao-2.jpg"
        },
        new POIModel
        {
            Id = 10,
            Name = "Lãng Quán",
            Description = "Quán nhậu mở cửa đến 4 giờ sáng.",
            Latitude = 10.7611499881882,
            Longitude = 106.705384011963,
            Address = "531 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Hotpot",
            ImageUrl = "/images/poi/lang-quan.jpg"
        },
        new POIModel
        {
            Id = 11,
            Name = "Ớt Xiêm Quán",
            Description = "Quán nổi tiếng với các món ăn cực cay.",
            Latitude = 10.7611852360527,
            Longitude = 106.705703610392,
            Address = "568 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Hotpot",
            ImageUrl = "/images/poi/ot-xiem.jpg"
        },
        new POIModel
        {
            Id = 12,
            Name = "Bún Cá Châu Đốc Dì Tư",
            Description = "Quán bún cá miền Tây nổi tiếng.",
            Latitude = 10.761123552507,
            Longitude = 106.706606909857,
            Address = "320/79 Vĩnh Khánh, Phường 9, Quận 4, TP.HCM",
            CategoryName = "Noodle",
            ImageUrl = "/images/poi/bun-ca.jpg"
        }
    };

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
