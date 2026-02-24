# 🚀 Hướng dẫn Setup Supabase cho VK StreetFood

## Bước 1: Tạo Project Supabase

1. Truy cập: https://supabase.com
2. Sign up với GitHub
3. Click **"New project"**:
   - **Organization**: Chọn organization của bạn
   - **Name**: `vk-streetfood`
   - **Database Password**: Tạo mật khẩu mạnh (GHI LẠI!)
   - **Region**: `Southeast Asia (Singapore)`
   - **Pricing Plan**: `Free`
4. Click **"Create new project"**
5. Đợi ~2 phút project khởi tạo ✨

## Bước 2: Lấy Connection String

1. Vào project vừa tạo
2. Click biểu tượng **Settings** (bánh răng) ở sidebar trái
3. Chọn **Database**
4. Scroll xuống phần **Connection string**
5. Chọn tab **URI** hoặc **Connection pooling**
6. Copy connection string có dạng:
   ```
   postgresql://postgres.xxxxx:YOUR-PASSWORD@aws-0-ap-southeast-1.pooler.supabase.com:6543/postgres
   ```

## Bước 3: Cập nhật Connection String

1. Mở file: `src/Server/VK.API/appsettings.Development.json`

2. Thay thế connection string:

**Nếu dùng Connection pooling (Recommended):**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-0-ap-southeast-1.pooler.supabase.com;Database=postgres;Username=postgres.xxxxx;Password=YOUR_PASSWORD;Port=6543;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

**Hoặc dùng Direct connection:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.xxxxx.supabase.co;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;Port=5432;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

⚠️ **LƯU Ý**: Thay `YOUR_PASSWORD` bằng password bạn đã tạo ở Bước 1!

## Bước 4: Tạo Database Migrations

Chạy lệnh sau trong terminal:

```powershell
dotnet ef migrations add InitialCreate --project src/Server/VK.Infrastructure/VK.Infrastructure.csproj --startup-project src/Server/VK.API/VK.API.csproj --output-dir Migrations
```

## Bước 5: Apply Migrations lên Supabase

```powershell
dotnet ef database update --project src/Server/VK.Infrastructure/VK.Infrastructure.csproj --startup-project src/Server/VK.API/VK.API.csproj
```

## Bước 6: Seed Data (Optional)

Sau khi migrations chạy xong, seed data:

```csharp
// Trong Program.cs, thêm trước app.Run():
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VKStreetFoodDbContext>();
    await VK.Infrastructure.Seeds.DatabaseSeeder.SeedAsync(context);
}
```

## Bước 7: Xem Database trên Supabase

1. Vào Supabase project
2. Click **Table Editor** ở sidebar
3. Xem các tables: `PointsOfInterest`, `AudioContents`, `Vendors`, etc.
4. Click vào từng table để xem data 🎉

## 🎁 Bonus: File Storage cho Audio & Images

Supabase có storage miễn phí:

1. Click **Storage** ở sidebar
2. Create bucket: `audio` và `images`
3. Set public access
4. Upload files và get public URLs

## ❓ Troubleshooting

### Lỗi connection timeout:

- Kiểm tra firewall/VPN
- Thử dùng connection pooling URL

### Lỗi password authentication:

- Đảm bảo password không có ký tự đặc biệt chưa escape
- Thử reset password trên Supabase

### Lỗi SSL:

- Thêm `Trust Server Certificate=true` vào connection string

## 🔗 Useful Links

- **Supabase Dashboard**: https://supabase.com/dashboard
- **Supabase Docs**: https://supabase.com/docs
- **Table Editor**: Your project → Table Editor
- **SQL Editor**: Your project → SQL Editor (để chạy SQL queries)

---

**Ready?** Bắt đầu từ Bước 1! 🚀
