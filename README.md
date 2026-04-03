# VK StreetFood - Nền tảng Du lịch Ẩm thực Phố Vĩnh Khánh

<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![MAUI](https://img.shields.io/badge/MAUI-Android%2FiOS-512BD4?style=flat-square&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Supabase-4169E1?style=flat-square&logo=postgresql&logoColor=white)

**Hệ thống du lịch ẩm thực thông minh với tính năng thuyết minh tự động dựa trên vị trí GPS**

[Tính năng](#-tính-năng-chính) • [Cài đặt](#-cài-đặt-nhanh) • [Kiến trúc](#-kiến-trúc-hệ-thống) • [API Documentation](#-api-endpoints) • [Đóng góp](#-đóng-góp)

</div>

---

## 📋 Mục lục

- [Giới thiệu](#-giới-thiệu)
- [Tính năng chính](#-tính-năng-chính)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Kiến trúc hệ thống](#-kiến-trúc-hệ-thống)
- [Yêu cầu hệ thống](#-yêu-cầu-hệ-thống)
- [Cài đặt nhanh](#-cài-đặt-nhanh)
- [Cấu hình](#%EF%B8%8F-cấu-hình)
- [Chạy ứng dụng](#-chạy-ứng-dụng)
- [API Endpoints](#-api-endpoints)
- [Database Schema](#-database-schema)
- [Tài khoản & Phân quyền](#-tài-khoản--phân-quyền)
- [Testing](#-testing)
- [Deployment](#-deployment)
- [Troubleshooting](#-troubleshooting)
- [Roadmap](#-roadmap)
- [Đóng góp](#-đóng-góp)
- [License](#-license)

---

## 🎯 Giới thiệu

**VK StreetFood** là nền tảng du lịch ẩm thực thông minh cho **Phố Vĩnh Khánh** (Quận 4, TP.HCM), giúp du khách khám phá và trải nghiệm văn hóa ẩm thực đường phố một cách độc đáo thông qua công nghệ.

### Vấn đề giải quyết

- ❌ Du khách khó tìm quán ăn nổi tiếng trong khu phố đông đúc
- ❌ Thiếu thông tin về nguồn gốc món ăn, lịch sử quán
- ❌ Không biết món đặc trưng của từng quán
- ❌ Trải nghiệm khám phá ẩm thực còn bị động

### Giải pháp

✅ **Thuyết minh tự động** khi đến gần điểm (geofence trigger)  
✅ **Bản đồ tương tác** hiển thị POI với thông tin chi tiết  
✅ **Đa ngôn ngữ** (Việt/Anh/Hàn) với fallback thông minh  
✅ **Hoạt động offline** với cache dữ liệu và audio  
✅ **QR code** để truy cập nhanh thông tin quán  
✅ **Dashboard quản trị** cho admin và chủ quán

---

## 🚀 Tính năng chính

### 📱 Mobile App (Du khách)

<table>
<tr>
<td width="50%">

#### Bản đồ & Định vị

- 🗺️ Hiển thị POI trên OpenStreetMap
- 📍 Theo dõi GPS thời gian thực
- 🎯 Geofence tự động phát audio
- 📏 Tính khoảng cách đến điểm

</td>
<td width="50%">

#### Nội dung & Trải nghiệm

- 🔊 Audio guide đa ngôn ngữ
- 📷 Ảnh quán, món ăn chi tiết
- ⭐ Đánh giá & yêu thích
- 📖 Lịch sử tham quan

</td>
</tr>
<tr>
<td>

#### QR & Deep Link

- 📲 Quét QR mở chi tiết quán
- 🔗 Deep link navigation
- ⚡ Truy cập nhanh từ poster

</td>
<td>

#### Offline & Performance

- 💾 Cache POI, route, audio
- 🌐 Map offline (.mbtiles)
- 🎵 Audio warmup/hotset
- 📦 Route package (.json)

</td>
</tr>
</table>

### 🖥️ Web Admin

#### Dashboard KPI

- 📊 Thống kê POI, visits, ratings
- 📈 Trend analysis & heatmap
- 🎯 Top POIs theo lượt truy cập
- 🔊 Audio coverage theo ngôn ngữ

#### Quản lý nội dung

- 🏪 CRUD POI, tour, categories
- 🎙️ Quản lý audio & translations
- 🖼️ Upload & quản lý media
- 🌍 Localization management

#### Quản trị Owner

- 👥 Đăng ký & duyệt chủ quán
- ✅ Approve/reject requests
- 📝 Content moderation workflow
- 🔐 Role-based access control

### 🛠️ API Backend

#### Core APIs

- **POI**: List, nearby, detail, categories
- **Tourist**: Device registration, location tracking, visit history
- **Audio**: Multi-language, TTS on-demand, batch generation
- **Tour**: Tour list, detail, waypoints
- **Analytics**: Event logging, statistics, dashboard data

#### Special Features

- 🔄 TTS on-demand với task deduplication
- 🌐 Localization hotset & warmup
- 📦 Offline package upload/download
- 🔍 Admin health check & coverage stats

---

## 💻 Công nghệ sử dụng

### Backend Stack

```
┌─────────────────────────────────────────────────────────┐
│  .NET 10 • ASP.NET Core Web API • EF Core • Npgsql    │
│  Swashbuckle (Swagger) • Session-based Auth            │
└─────────────────────────────────────────────────────────┘
```

### Mobile Stack

```
┌─────────────────────────────────────────────────────────┐
│  .NET MAUI (Android/iOS)                                │
│  Mapsui + OpenStreetMap • ZXing.Net.Maui (QR)         │
│  Plugin.Maui.Audio • sqlite-net-pcl                    │
│  CommunityToolkit.Mvvm • CommunityToolkit.Maui         │
└─────────────────────────────────────────────────────────┘
```

### Database & Infrastructure

```
┌─────────────────────────────────────────────────────────┐
│  PostgreSQL (Supabase) • SQL Migrations                │
│  Row Level Security (RLS) • Automated Seeding          │
└─────────────────────────────────────────────────────────┘
```

### Development Tools

- **Version Control**: Git
- **IDE**: Visual Studio 2022 / VS Code
- **API Testing**: Swagger UI, HTTP files
- **Database**: Supabase Dashboard, pgAdmin

---

## 🏗️ Kiến trúc hệ thống

### Solution Structure

```
VK_StreetFood/
│
├── 📱 src/Client/
│   └── VK.Mobile/                    # MAUI app for tourists
│       ├── Views/                    # XAML pages
│       ├── ViewModels/               # MVVM ViewModels
│       ├── Services/                 # API, Location, Audio, Geofence
│       ├── Models/                   # Data models
│       └── Resources/                # Images, fonts, strings
│
├── 🔧 src/Server/
│   ├── VK.API/                       # REST API
│   │   ├── Controllers/              # API endpoints
│   │   ├── Services/                 # Business logic
│   │   └── wwwroot/                  # Static files (audio, images)
│   │
│   ├── VK.Web/                       # Admin/Owner web portal
│   │   ├── Controllers/              # MVC controllers
│   │   ├── Views/                    # Razor views
│   │   └── Services/                 # Web services
│   │
│   ├── VK.Core/                      # Domain entities
│   │   └── Entities/                 # POI, Tour, Audio, Tourist, etc.
│   │
│   └── VK.Infrastructure/            # Data access
│       ├── Data/                     # DbContext, configurations
│       └── Repositories/             # Data repositories
│
├── 🔄 src/Shared/
│   ├── VK.Contracts/                 # Request/Response DTOs
│   └── VK.Shared/                    # Constants, enums, shared DTOs
│
├── 🗄️ supabase/
│   ├── migrations/                   # SQL migration files
│   │   └──delta_align_existing_db.sql
│   ├── schema.sql
│   ├── rls.sql
│   ├── seed_pois.sql
│   └── MIGRATION_INSTRUCTIONS.md
│
├── 🧪 tests/
│   ├── VK.API.Tests/
│   └── VK.Core.Tests/
│
├── 🖼️ images/                         # Asset images
│   ├── poi/                          # POI images
│   └── backgroundad/                 # Background ads
│
└── 📄 docs/
    └── prd.md                        # Product Requirements Document
```

### Architecture Diagram

```
┌────────────────────────────────────────────────────────────────┐
│                     FRONTEND LAYER                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │ Mobile MAUI  │  │  Admin Web   │  │  Owner Web   │        │
│  │  (Visitor)   │  │  (ASP.NET)   │  │  (ASP.NET)   │        │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘        │
└─────────┼──────────────────┼──────────────────┼────────────────┘
          │                  │                  │
          │        HTTPS REST + Cookie Auth     │
          ▼                  ▼                  ▼
┌────────────────────────────────────────────────────────────────┐
│                      BACKEND LAYER                             │
│         ┌────────────────────────────────────────┐             │
│         │   ASP.NET Core Web API (.NET 10)      │             │
│         │   ┌────────────────────────────┐      │             │
│         │   │  Controllers               │      │             │
│         │   │  ├─ POI                    │      │             │
│         │   │  ├─ Tourist                │      │             │
│         │   │  ├─ Audio                  │      │             │
│         │   │  ├─ Tour                   │      │             │
│         │   │  ├─ Analytics              │      │             │
│         │   │  └─ Admin                  │      │             │
│         │   └────────────┬───────────────┘      │             │
│         │                ▼                       │             │
│         │   ┌────────────────────────────┐      │             │
│         │   │  Services                  │      │             │
│         │   │  ├─ TtsGenerationService   │      │             │
│         │   │  └─ AudioTaskManager       │      │             │
│         │   └────────────┬───────────────┘      │             │
│         │                ▼                       │             │
│         │   ┌────────────────────────────┐      │             │
│         │   │  Repositories              │      │             │
│         │   └────────────┬───────────────┘      │             │
│         │                ▼                       │             │
│         │   ┌────────────────────────────┐      │             │
│         │   │  EF Core + DbContext       │      │             │
│         │   └────────────────────────────┘      │             │
│         └────────────────┬───────────────────────┘             │
└──────────────────────────┼─────────────────────────────────────┘
                           │
          ┌────────────────┴────────────────┐
          ▼                                 ▼
┌──────────────────────┐        ┌──────────────────────┐
│   PostgreSQL         │        │   Static Files       │
│   (Supabase)         │        │   wwwroot/           │
│                      │        │   ├─ audio/          │
│   ├─ pois            │        │   ├─ images/         │
│   ├─ tours           │        │   └─ offline/        │
│   ├─ audio_contents  │        │      ├─ *.mbtiles    │
│   ├─ tourists        │        │      └─ *.json       │
│   ├─ categories      │        └──────────────────────┘
│   ├─ poi_owners      │
│   └─ analytics       │
└──────────────────────┘
```

### Data Flow

#### 1️⃣ Tourist Opens App

```
Mobile App → GET /api/pois → API → Database
                ↓
           Render Map + Cache POI locally
```

#### 2️⃣ Geofence Trigger

```
GPS Update → Geofence Check → Distance < Radius
                ↓
           Select Audio by Language
                ↓
           Play from Cache OR Download → Save → Play
```

#### 3️⃣ QR Code Scan

```
QR Scan → Parse Deep Link → Navigate to POI Detail
              ↓
         GET /api/pois/{id}/detail
              ↓
         Display Info + Images + Menu + Audio
```

#### 4️⃣ Owner Updates Content

```
Login (Cookie Auth) → Edit POI → Submit Request
         ↓
    Status: Pending
         ↓
    Admin Reviews → Approve/Reject
         ↓
    Update Database → Tourists see new data on next sync
```

---

## ⚙️ Yêu cầu hệ thống

### Bắt buộc

- ✅ **.NET SDK 10.0** ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- ✅ **Git** ([Download](https://git-scm.com/downloads))
- ✅ **PostgreSQL/Supabase** account với project đã tạo

### Cho Mobile Development

- ✅ **Visual Studio 2022** (v17.12+) với workload:
  - ☑️ .NET Multi-platform App UI development
  - ☑️ Mobile development with .NET
- ✅ **Android SDK** (API level 21+)
- ✅ **iOS SDK** (nếu build cho iOS trên macOS)

### Cho Web Development (Optional)

- ✅ **Node.js** v20+ (nếu sử dụng React frontend)
- ✅ **VS Code** với C# extension

### Khuyến nghị

- 🔹 **RAM**: 8GB+ (16GB tốt hơn cho MAUI)
- 🔹 **Storage**: 10GB+ free space
- 🔹 **OS**: Windows 10/11, macOS 12+, hoặc Linux (Ubuntu 20.04+)

---

## 🚀 Cài đặt nhanh

### 1️⃣ Clone Repository

```bash
git clone https://github.com/your-org/VK_StreetFood.git
cd VK_StreetFood
```

### 2️⃣ Restore Dependencies

```bash
dotnet restore VKStreetFood.slnx
```

### 3️⃣ Setup Database

#### A. Tạo Supabase Project

1. Đăng nhập [Supabase](https://supabase.com/)
2. Tạo project mới
3. Copy **Connection String** từ Settings → Database

#### B. Chạy Migrations

Mở **Supabase SQL Editor** và chạy lần lượt:

```sql
-- Step 1: Create schema and tables
-- Copy nội dung file: supabase/migrations/schema.sql

-- Step 2: Create Row Level Security policies
-- Copy nội dung file: supabase/migrations/rls.sql

-- Step 3: Seed initial data
-- Copy nội dung file: supabase/migrations/seed_pois.sql
```

📖 **Chi tiết**: Xem `supabase/MIGRATION_INSTRUCTIONS.md`

### 4️⃣ Cấu hình Connection String

Cập nhật trong các file sau:

#### `src/Server/VK.API/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_HOST;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD"
  }
}
```

#### `src/Server/VK.Web/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_HOST;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD"
  }
}
```

> ⚠️ **Security**: Không commit credentials lên Git! Sử dụng User Secrets hoặc Environment Variables.

---

## ⚡ Chạy ứng dụng

### 🔧 API Backend

```bash
cd src/Server/VK.API
dotnet run
```

✅ API chạy tại: `http://localhost:5089`  
📖 Swagger UI: `http://localhost:5089/swagger`

### 🌐 Web Admin/Owner

Mở terminal mới:

```bash
cd src/Server/VK.Web
dotnet run
```

✅ Web portal chạy tại: `https://localhost:7xxx` (port tự động)

### 📱 Mobile App

#### Option 1: Visual Studio

1. Mở `VKStreetFood.slnx`
2. Set `VK.Mobile` làm startup project
3. Chọn target:
   - **Android Emulator**: API 21+
   - **Android Device**: Kết nối qua USB, bật USB debugging
   - **iOS Simulator**: (macOS only)
4. Nhấn **F5** để chạy

#### Option 2: CLI

```bash
cd src/Client/VK.Mobile

# Android
dotnet build -t:Run -f net10.0-android

# iOS (macOS only)
dotnet build -t:Run -f net10.0-ios
```

#### Cấu hình API Base URL

Sửa file `src/Client/VK.Mobile/Models/AppSettings.cs`:

```csharp
public static class AppSettings
{
    // Android Emulator
    public const string ApiBaseUrl = "http://10.0.2.2:5089/api/";

    // Thiết bị thật (thay YOUR_IP bằng IP máy dev)
    // public const string ApiBaseUrl = "http://192.168.1.10:5089/api/";

    // Production
    // public const string ApiBaseUrl = "https://api.vkstreetfood.com/api/";
}
```

📌 **Lưu ý**:

- Android Emulator **PHẢI** dùng `10.0.2.2` thay vì `localhost`
- Thiết bị thật phải cùng mạng WiFi với máy dev
- Bật firewall port 5089 nếu cần

---

## 🛠️ Cấu hình

### API Configuration

File: `src/Server/VK.API/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Web Configuration

File: `src/Server/VK.Web/appsettings.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5089/api/"
  },
  "AdminAuth": {
    "Username": "admin",
    "Password": "Admin@123" // ⚠️ ĐỔI PASSWORD PRODUCTION!
  },
  "Session": {
    "IdleTimeout": "00:30:00",
    "CookieName": ".VKStreetFood.Session"
  }
}
```

### Mobile Configuration

File: `src/Client/VK.Mobile/Models/AppSettings.cs`

```csharp
public static class AppSettings
{
    public const string ApiBaseUrl = "http://10.0.2.2:5089/api/";

    // Geofence settings
    public const double DefaultGeofenceRadius = 50.0; // meters
    public const double LocationUpdateInterval = 5.0; // seconds

    // Audio settings
    public const string DefaultLanguage = "vi";
    public const double AudioFadeOutDuration = 2.0; // seconds

    // Cache settings
    public const int MaxCachedAudios = 50;
    public const int CacheExpirationDays = 7;
}
```

---

## 📚 API Endpoints

### 📍 POI APIs

| Method | Endpoint                | Description        | Auth |
| ------ | ----------------------- | ------------------ | ---- |
| GET    | `/api/pois`             | Lấy danh sách POI  | ❌   |
| GET    | `/api/pois/nearby`      | Tìm POI gần vị trí | ❌   |
| GET    | `/api/pois/{id}`        | Chi tiết POI       | ❌   |
| GET    | `/api/pois/categories`  | Danh mục POI       | ❌   |
| GET    | `/api/pois/{id}/images` | Ảnh của POI        | ❌   |
| GET    | `/api/pois/{id}/menu`   | Menu món ăn        | ❌   |

**Example Request:**

```bash
curl -X GET "http://localhost:5089/api/pois/nearby?lat=10.7553&lng=106.6986&radius=500"
```

**Example Response:**

```json
{
  "pois": [
    {
      "id": "poi-001",
      "name": "Bánh Mì Huỳnh Hoa",
      "latitude": 10.7553,
      "longitude": 106.6986,
      "category": "Bánh mì",
      "rating": 4.5,
      "distance": 125.5
    }
  ]
}
```

### 👤 Tourist APIs

| Method | Endpoint                     | Description       | Auth |
| ------ | ---------------------------- | ----------------- | ---- |
| POST   | `/api/tourists/register`     | Đăng ký device    | ❌   |
| PUT    | `/api/tourists/location`     | Cập nhật vị trí   | ❌   |
| POST   | `/api/tourists/visit`        | Ghi nhận visit    | ❌   |
| POST   | `/api/tourists/favorites`    | Thêm yêu thích    | ❌   |
| GET    | `/api/tourists/{id}/history` | Lịch sử tham quan | ❌   |

### 🔊 Audio APIs

| Method | Endpoint                    | Description             | Auth     |
| ------ | --------------------------- | ----------------------- | -------- |
| GET    | `/api/audio/{poiId}/{lang}` | Lấy audio theo ngôn ngữ | ❌       |
| POST   | `/api/audio/tts`            | Generate TTS on-demand  | ✅ Admin |
| POST   | `/api/audio/generate-batch` | Generate batch audio    | ✅ Admin |
| GET    | `/api/audio/status`         | Audio coverage status   | ✅ Admin |

### 🗺️ Tour APIs

| Method | Endpoint                    | Description          | Auth |
| ------ | --------------------------- | -------------------- | ---- |
| GET    | `/api/tours`                | Danh sách tour       | ❌   |
| GET    | `/api/tours/{id}`           | Chi tiết tour        | ❌   |
| GET    | `/api/tours/{id}/waypoints` | Điểm dừng trong tour | ❌   |

### 📊 Analytics APIs

| Method | Endpoint                   | Description     | Auth     |
| ------ | -------------------------- | --------------- | -------- |
| POST   | `/api/analytics/event`     | Ghi event       | ❌       |
| GET    | `/api/analytics/dashboard` | Dashboard stats | ✅ Admin |
| GET    | `/api/analytics/top-pois`  | Top POIs        | ✅ Admin |
| GET    | `/api/analytics/trends`    | Xu hướng        | ✅ Admin |

### 📦 Offline APIs

| Method | Endpoint                     | Description           | Auth     |
| ------ | ---------------------------- | --------------------- | -------- |
| GET    | `/api/offline/map-package`   | Download map .mbtiles | ❌       |
| POST   | `/api/offline/map-package`   | Upload map package    | ✅ Admin |
| GET    | `/api/offline/route-package` | Download route .json  | ❌       |
| POST   | `/api/offline/route-package` | Upload route package  | ✅ Admin |
| GET    | `/api/offline/map-status`    | Map package status    | ❌       |

### 🔧 Admin APIs

| Method | Endpoint                    | Description       | Auth     |
| ------ | --------------------------- | ----------------- | -------- |
| GET    | `/api/admin/health`         | Health check      | ✅ Admin |
| GET    | `/api/admin/stats`          | System statistics | ✅ Admin |
| POST   | `/api/admin/owners/approve` | Duyệt chủ quán    | ✅ Admin |
| POST   | `/api/admin/owners/reject`  | Từ chối chủ quán  | ✅ Admin |

📖 **Full API Documentation**: Xem Swagger UI tại `http://localhost:5089/swagger`

---

## 🗄️ Database Schema

### Core Tables

#### `pois` (Points of Interest)

```sql
id              UUID PRIMARY KEY
name            TEXT NOT NULL
name_en         TEXT
name_ko         TEXT
description     TEXT
description_en  TEXT
description_ko  TEXT
latitude        DOUBLE PRECISION NOT NULL
longitude       DOUBLE PRECISION NOT NULL
category_id     UUID REFERENCES categories(id)
geofence_radius DOUBLE PRECISION DEFAULT 50
qr_code         TEXT UNIQUE
rating          DECIMAL(3,2)
is_active       BOOLEAN DEFAULT true
created_at      TIMESTAMP WITH TIME ZONE
updated_at      TIMESTAMP WITH TIME ZONE
```

#### `audio_contents`

```sql
id              UUID PRIMARY KEY
poi_id          UUID REFERENCES pois(id)
language_code   VARCHAR(5) NOT NULL
audio_url       TEXT
transcript      TEXT
duration_seconds INTEGER
is_generated    BOOLEAN DEFAULT false
created_at      TIMESTAMP WITH TIME ZONE
```

#### `tourists`

```sql
id              UUID PRIMARY KEY
device_id       TEXT UNIQUE NOT NULL
device_name     TEXT
preferred_language VARCHAR(5) DEFAULT 'vi'
last_latitude   DOUBLE PRECISION
last_longitude  DOUBLE PRECISION
created_at      TIMESTAMP WITH TIME ZONE
last_active_at  TIMESTAMP WITH TIME ZONE
```

#### `tours`

```sql
id              UUID PRIMARY KEY
name            TEXT NOT NULL
name_en         TEXT
name_ko         TEXT
description     TEXT
duration_minutes INTEGER
difficulty_level VARCHAR(20)
is_featured     BOOLEAN DEFAULT false
created_at      TIMESTAMP WITH TIME ZONE
```

#### `categories`

```sql
id              UUID PRIMARY KEY
name            TEXT UNIQUE NOT NULL
name_en         TEXT
name_ko         TEXT
icon_url        TEXT
display_order   INTEGER
```

#### `poi_owners`

```sql
id              UUID PRIMARY KEY
username        TEXT UNIQUE NOT NULL
password_hash   TEXT NOT NULL
full_name       TEXT
email           TEXT
phone           TEXT
status          VARCHAR(20) DEFAULT 'pending'
is_verified     BOOLEAN DEFAULT false
created_at      TIMESTAMP WITH TIME ZONE
```

### Relationship Diagram

```
categories ──┐
             ├──< pois >──┬── audio_contents
             │            │
tours ───────┘            ├── tour_pois
                          │
                          ├── visit_history ──< tourists
                          │
                          ├── favorites ──< tourists
                          │
                          └── poi_images

poi_owners ──< owner_pois
```

📖 **Full Schema**: Xem `supabase/migrations/schema.sql`

---

## 👥 Tài khoản & Phân quyền

### Roles

| Role          | Description            | Permissions                                              |
| ------------- | ---------------------- | -------------------------------------------------------- |
| **admin**     | Quản trị viên hệ thống | Full access: quản lý POI, users, audio, analytics        |
| **poi_owner** | Chủ quán               | Chỉnh sửa POI của mình, upload ảnh/audio (cần duyệt)     |
| **tourist**   | Du khách               | Xem POI, nghe audio, lưu favorites (không cần đăng nhập) |

### Default Admin Account

**Web Admin Portal** (`/Home/Login`)

```
Username: admin
Password: Admin@123
```

> ⚠️ **QUAN TRỌNG**: Đổi mật khẩu ngay sau khi deploy production!

Cấu hình trong `src/Server/VK.Web/appsettings.json`:

```json
{
  "AdminAuth": {
    "Username": "admin",
    "Password": "YOUR_SECURE_PASSWORD"
  }
}
```

### Owner Registration Flow

1. **Đăng ký**: Chủ quán điền form `/Owner/Register`
   - Username, password, email, phone
   - Chọn POI liên kết (optional)
2. **Pending**: Hệ thống tạo account với `status = 'pending'`

3. **Admin Review**: Admin vào dashboard `/Admin/Owners`
   - Xem danh sách pending owners
   - Kiểm tra thông tin
4. **Approve/Reject**:
   - ✅ Approve → `status = 'approved'`, `is_verified = true`
   - ❌ Reject → `status = 'rejected'`

5. **Owner Access**: Sau khi approved, owner login và:
   - Chỉnh sửa POI của mình
   - Upload ảnh, audio (qua request workflow)
   - Xem analytics của quán

### Content Moderation Workflow

```
Owner Submit Edit
       ↓
  Status: Pending
       ↓
Admin Review Dashboard
       ↓
   Approve ────→ Update POI ────→ Tourist thấy nội dung mới
       │
   Reject ────→ Notify Owner ───→ Owner có thể submit lại
```

---

## 🧪 Testing

### Run All Tests

```bash
dotnet test VKStreetFood.slnx
```

### Run Specific Test Project

```bash
dotnet test tests/VK.API.Tests/VK.API.Tests.csproj
dotnet test tests/VK.Core.Tests/VK.Core.Tests.csproj
```

### Test Coverage

> ⚠️ **Note**: Test projects hiện đang ở giai đoạn khởi tạo mẫu. Cần bổ sung test cases cho:
>
> - POI business logic
> - Audio generation & fallback
> - Geofence triggers
> - Owner approval workflow
> - Analytics calculations

**Roadmap**: Xem [Testing Strategy](#roadmap)

---

## 🚢 Deployment

### Production Checklist

#### 🔐 Security

- [ ] Đổi admin password mạnh
- [ ] Enable HTTPS cho tất cả endpoints
- [ ] Rotate database credentials
- [ ] Remove sensitive data từ appsettings.json → Environment Variables
- [ ] Enable CORS chỉ cho domains production
- [ ] Review và harden RLS policies

#### 🗄️ Database

- [ ] Run migrations trên production database
- [ ] Setup automated backups (daily)
- [ ] Monitor connection pooling
- [ ] Index optimization cho queries hay dùng

#### 📦 Assets

- [ ] Upload offline packages (map.mbtiles, routes.json) vào `wwwroot/offline/`
- [ ] Generate TTS batch audio cho tất cả POIs
- [ ] Optimize images (compress, WebP format)
- [ ] Setup CDN cho static files

#### 🔍 Monitoring

- [ ] Configure structured logging (Serilog, Application Insights)
- [ ] Setup health check endpoints
- [ ] Monitor API error rate & latency
- [ ] Track audio playback success rate

#### ⚡ Performance

- [ ] Enable response compression
- [ ] Setup caching (Redis) cho frequently accessed data
- [ ] Optimize EF Core queries (AsNoTracking where appropriate)
- [ ] Load testing với expected traffic

### Deploy to Azure (Example)

#### API Backend

```bash
# Build
dotnet publish src/Server/VK.API/VK.API.csproj -c Release -o ./publish/api

# Deploy to Azure App Service
az webapp deployment source config-zip \
  --resource-group VKStreetFood-RG \
  --name vkstreetfood-api \
  --src ./publish/api.zip
```

#### Mobile App

**Android (Google Play):**

1. Build Release APK/AAB:
   ```bash
   dotnet publish -f net10.0-android -c Release
   ```
2. Sign with keystore
3. Upload to Google Play Console

**iOS (App Store):**

1. Build Archive (macOS):
   ```bash
   dotnet publish -f net10.0-ios -c Release
   ```
2. Submit via Xcode / Transporter

### Environment Variables

Setup trên hosting platform:

```bash
ConnectionStrings__DefaultConnection=<production_db_connection>
AdminAuth__Password=<strong_password>
Logging__LogLevel__Default=Warning
```

---

## 🐛 Troubleshooting

### Common Issues

#### 1. API không kết nối được từ Mobile

**Triệu chứng:**

```
Network request failed
```

**Giải pháp:**

- ✅ Android Emulator: Dùng `http://10.0.2.2:5089/api/` thay vì `localhost`
- ✅ iOS Simulator: Có thể dùng `http://localhost:5089/api/`
- ✅ Thiết bị thật: Dùng IP máy dev (VD: `http://192.168.1.10:5089/api/`)
- ✅ Kiểm tra firewall Windows cho phép port 5089
- ✅ API phải đang chạy trước khi test mobile

```bash
# Check API đang chạy
curl http://localhost:5089/api/pois
```

#### 2. Database Migration Failed

**Triệu chứng:**

```
Table 'pois' does not exist
```

**Giải pháp:**

1. Kiểm tra connection string đúng
2. Chạy lại migrations theo thứ tự:
   ```sql
   -- schema.sql → Tạo tables
   -- rls.sql → RLS policies
   -- seed_pois.sql → Seed data
   ```
3. Verify trong Supabase Table Editor

#### 3. Audio không phát

**Triệu chứng:**

- Không có âm thanh khi geofence trigger
- Lỗi "Audio file not found"

**Giải pháp:**

- ✅ Kiểm tra bảng `audio_contents` có records cho POI & ngôn ngữ
- ✅ Verify `audio_url` trỏ đúng file trong `wwwroot/audio/`
- ✅ File MP3 phải tồn tại và không corrupt
- ✅ Permissions đọc file trên server

```sql
-- Check audio coverage
SELECT poi_id, language_code, audio_url
FROM audio_contents
WHERE poi_id = 'your-poi-id';
```

#### 4. Geofence không trigger

**Triệu chứng:**

- Đã vào vùng POI nhưng không phát audio

**Giải pháp:**

- ✅ Kiểm tra GPS permissions được grant
- ✅ Verify `geofence_radius` trong database (VD: 50m)
- ✅ Test với bán kính lớn hơn (100m) trước
- ✅ Check location service đang bật

```csharp
// Debug trong GeofenceEngine.cs
Console.WriteLine($"Distance to POI: {distance}m, Radius: {radius}m");
```

#### 5. Web Admin không gọi được API

**Triệu chứng:**

```
CORS policy: No 'Access-Control-Allow-Origin' header
```

**Giải pháp:**

`VK.Web/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5089/api/"
  }
}
```

#### 6. Build Mobile Failed

**Triệu chứng:**

```
Error: The Android SDK is not installed
```

**Giải pháp:**

1. Mở Visual Studio Installer
2. Modify → ☑️ Mobile development with .NET
3. Install Android SDK (API 21+)
4. Restart Visual Studio

#### 7. Owner không thể login

**Triệu chứng:**

- Login failed với credentials đúng

**Giải pháp:**

```sql
-- Check owner status
SELECT username, status, is_verified
FROM poi_owners
WHERE username = 'owner_username';

-- If pending, admin cần approve:
UPDATE poi_owners
SET status = 'approved', is_verified = true
WHERE username = 'owner_username';
```

### Debug Mode

Enable verbose logging:

`appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "VK.API": "Debug",
      "VK.Infrastructure": "Debug"
    }
  }
}
```

---

## 🗺️ Roadmap

### Phase 1: POC ✅ (Current)

- [x] Core API endpoints
- [x] Mobile MAUI app với geofence
- [x] Web admin dashboard
- [x] Audio guide đa ngôn ngữ
- [x] Offline support (map + audio cache)
- [x] Owner registration & approval workflow

### Phase 2: Enhancement 🚧 (In Progress)

- [ ] **Testing**: Unit tests & integration tests coverage
- [ ] **Analytics**: Advanced heatmap & trend visualization
- [ ] **Localization**: Thêm tiếng Nhật, Trung
- [ ] **Performance**: Redis caching layer
- [ ] **Mobile**: Push notifications cho tour suggestions

### Phase 3: Scale 📅 (Planned)

- [ ] **Multi-region**: Mở rộng sang khu phố khác (Quận 1, Quận 5)
- [ ] **AI Features**:
  - Gợi ý quán dựa trên preference & history
  - AR navigation overlay
  - Voice-controlled tour guide
- [ ] **Social Features**:
  - Tourist reviews & ratings
  - Share experiences on social media
  - Leaderboard & gamification
- [ ] **Business Features**:
  - Owner analytics dashboard
  - Promotion & ads management
  - Booking integration

### Phase 4: Advanced 🔮 (Future)

- [ ] **IoT Integration**: Beacon-based micro-location
- [ ] **Blockchain**: NFT collectibles for landmarks
- [ ] **Metaverse**: Virtual tour experience
- [ ] **API Marketplace**: Open API for 3rd party integrations

---

## 🤝 Đóng góp

Tôi hoan nghênh mọi đóng góp từ cộng đồng!

### Quy trình Contribute

1. **Fork** repository
2. **Create** feature branch:
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Commit** changes:
   ```bash
   git commit -m "Add some amazing feature"
   ```
4. **Push** to branch:
   ```bash
   git push origin feature/amazing-feature
   ```
5. **Open** Pull Request

### Coding Standards

#### C# (.NET)

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use **PascalCase** cho classes, methods, properties
- Use **camelCase** cho local variables, parameters
- Add XML comments cho public APIs

```csharp
/// <summary>
/// Retrieves POIs near the specified location.
/// </summary>
/// <param name="latitude">GPS latitude</param>
/// <param name="longitude">GPS longitude</param>
/// <param name="radiusMeters">Search radius in meters</param>
/// <returns>List of nearby POIs</returns>
public async Task<List<POI>> GetNearbyPOIsAsync(
    double latitude,
    double longitude,
    double radiusMeters)
{
    // Implementation
}
```

#### SQL

- Use **snake_case** cho table & column names
- Add indexes cho foreign keys
- Include `created_at`, `updated_at` timestamps

#### Git Commit Messages

Format: `<type>(<scope>): <subject>`

**Types:**

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style (formatting, missing semicolons, etc.)
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Build process, dependencies

**Examples:**

```
feat(api): add geofence radius configuration endpoint
fix(mobile): resolve audio playback crash on iOS
docs(readme): update deployment instructions
```

### Areas for Contribution

🐛 **Bug Fixes**: Check Issues  
✨ **Features**: See [Roadmap](#roadmap)  
📖 **Documentation**: Improve README, add tutorials  
🧪 **Testing**: Write unit/integration tests  
🌐 **Localization**: Add new language support  
🎨 **UI/UX**: Improve mobile app design

---

## 📄 License

---

## 🙏 Acknowledgments

- **OpenStreetMap** cho dữ liệu bản đồ
- **Mapsui** cho map rendering engine
- **Supabase** cho PostgreSQL hosting
- **Microsoft** cho .NET MAUI framework
- Cộng đồng **Phố Vĩnh Khánh** đã hỗ trợ thu thập dữ liệu

---
