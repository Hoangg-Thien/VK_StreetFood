# VK Street Food - Server Management Scripts

## Quick Start

### Start Servers

Double-click `start-servers.bat` hoặc chạy:

```
start-servers.bat
```

Servers sẽ chạy **minimized** (thu nhỏ) trong background. Bạn có thể:

- ✅ Tắt terminal đi - servers vẫn chạy
- ✅ Truy cập bất cứ lúc nào qua browser
- ✅ Khởi động lại máy → chỉ cần double-click start-servers.bat lại

### Stop Servers

Double-click `stop-servers.bat` hoặc chạy:

```
stop-servers.bat
```

### Check Status

Double-click `check-servers.bat` để xem servers có đang chạy không

## Access URLs

- 📊 **Dashboard**: http://localhost:5117
- 📍 **POI Management**: http://localhost:5117/POI
- 📚 **API Docs**: http://localhost:5089/swagger

## Startup on Boot (Optional)

Để servers tự động start khi khởi động Windows:

1. Nhấn `Win + R`, gõ: `shell:startup`
2. Copy shortcut của `start-servers.bat` vào folder Startup
3. Done! Servers sẽ tự động chạy mỗi khi boot Windows

## Troubleshooting

### Port bị chiếm

Chạy `stop-servers.bat` trước, sau đó `start-servers.bat` lại

### Servers không start

1. Check xem .NET SDK đã cài chưa: `dotnet --version`
2. Check database connection trong appsettings.json

### Tìm minimized windows

Taskbar → tìm "VK API Server" hoặc "VK Web Portal" → click để restore

## Notes

- Servers chạy trong Development mode (Swagger UI enabled)
- Logs hiển thị trong minimized terminal windows
- Database: Supabase PostgreSQL (remote)
