# VK StreetFood - Nền tảng Du lịch Ẩm thực Phố Vĩnh Khánh

<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![MAUI](https://img.shields.io/badge/MAUI-Android%2FiOS-512BD4?style=flat-square&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Supabase-4169E1?style=flat-square&logo=postgresql&logoColor=white)

**Hệ thống du lịch ẩm thực thông minh với tính năng thuyết minh tự động dựa trên vị trí GPS**

[Tính năng](#-tính-năng-chính) • [Cài đặt](#-cài-đặt) • [Kiến trúc](#-kiến-trúc-hệ-thống) • [Deployment](#-deployment) • [Đóng góp](#-đóng-góp)

</div>

---

## 📋 Mục lục

- [Giới thiệu](#-giới-thiệu)
- [Tính năng chính](#-tính-năng-chính)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Kiến trúc hệ thống](#-kiến-trúc-hệ-thống)
- [Cài đặt](#-cài-đặt)
- [Cấu hình](#%EF%B8%8F-cấu-hình)
- [Chạy ứng dụng](#-chạy-ứng-dụng)
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
✅ **Đa ngôn ngữ** (Việt/Anh/Hàn) với TTS tự động  
✅ **Hoạt động offline** với cache dữ liệu và audio  
✅ **QR code** để truy cập nhanh thông tin quán  
✅ **Dashboard quản trị** cho admin và chủ quán  
✅ **Payment Integration** với QR code thanh toán

---

## 🚀 Tính năng chính

### 📱 Mobile App (.NET MAUI - Android/iOS)

#### Bản đồ & Định vị
- 🗺️ Hiển thị POI trên OpenStreetMap (Mapsui)
- 📍 Theo dõi GPS thời gian thực
- 🎯 Geofence tự động phát audio khi vào vùng POI
- 📏 Tính khoảng cách đến điểm
- 🌐 Hỗ trợ offline map (.mbtiles)

#### Nội dung & Trải nghiệm
- 🔊 Audio guide đa ngôn ngữ (vi, en, ko)
- 📷 Xem ảnh quán, món ăn chi tiết
- ⭐ Đánh giá & yêu thích POI
- 📖 Lịch sử tham quan
- 🏆 Tour gợi ý theo chủ đề

#### QR & Deep Link
- 📲 Quét QR code mở chi tiết quán (ZXing.Net.Maui)
- 💳 Quét QR thanh toán
- 🔗 Deep link navigation
- ⚡ Truy cập nhanh từ poster

#### Offline & Cache
- 💾 SQLite cache cho POI, routes, audio
- 🎵 Audio warmup với Plugin.Maui.Audio
- 📦 Route package download (.json)

### 🖥️ Web Portal (ASP.NET Core MVC)

#### Dashboard Admin
- 📊 Thống kê tổng quan (POI, visits, ratings, tourists)
- 📈 Phân tích xu hướng theo thời gian
- 🎯 Top POIs theo lượt truy cập
- 🔊 Coverage báo cáo audio theo ngôn ngữ
- 👥 Quản lý user & owner registration

#### Quản lý nội dung POI
- 🏪 CRUD POI, tours, categories, tags
- 🖼️ Upload & quản lý ảnh POI (Supabase Storage)
- 📝 Quản lý thông tin quán (địa chỉ, giờ mở cửa, vendor)
- 🌍 Translation management (PoiTranslations, TourTranslations)
- ✅ Content approval workflow

#### Quản lý Audio
- 🎙️ Upload audio files cho từng POI
- 🤖 TTS tự động với edge-tts (Text-to-Speech on-demand)
- 🌐 Quản lý audio theo ngôn ngữ
- 📊 Audio coverage statistics

#### Quản trị Owner
- 👤 Đăng ký chủ quán (PoiOwnerRegistrations)
- ✅ Approve/reject registration requests
- 📝 Content change request workflow (PoiContentChangeRequests)
- 🔐 Role-based access control (Admin/Owner)

#### Payment Management
- 💳 Cấu hình QR code thanh toán cho POI
- 💰 Quản lý thông tin VNPay/MoMo/ZaloPay
- 📊 Usage history tracking

### 🛠️ REST API Backend (ASP.NET Core Web API)

#### Core Endpoints

**POI Controller**
- `GET /api/pois` - Danh sách POI
- `GET /api/pois/nearby` - POI gần vị trí hiện tại
- `GET /api/pois/{id}` - Chi tiết POI
- `GET /api/pois/categories` - Danh mục POI

**Tourist Controller**
- `POST /api/tourists/register` - Đăng ký device
- `POST /api/tourists/{id}/location` - Cập nhật GPS
- `GET /api/tourists/{id}/history` - Lịch sử tham quan

**Audio Controller**
- `GET /api/audio/{poiId}/{languageCode}` - Lấy audio file
- `POST /api/audio/generate` - TTS on-demand generation
- `POST /api/audio/batch-generate` - Batch TTS cho nhiều POI
- `GET /api/audio/hotset` - Warmup audio cache

**Tour Controller**
- `GET /api/tours` - Danh sách tour
- `GET /api/tours/{id}` - Chi tiết tour với waypoints

**Analytics Controller**
- `POST /api/analytics/log` - Ghi nhận event
- `GET /api/analytics/stats` - Thống kê dashboard

**Offline Controller**
- `GET /api/offline/routes` - Package offline routes
- `GET /api/offline/audio/{languageCode}` - Batch audio download

**Localization Controller**
- `GET /api/localization/strings/{languageCode}` - App strings
- `GET /api/localization/warmup` - Warmup translations

**Payment Controller**
- `GET /api/payment/qr/{poiId}` - Lấy QR payment config

**Admin Controller**
- `GET /api/admin/health` - Health check & coverage stats

#### Features nổi bật
- 🔄 **TTS on-demand** với task deduplication (AudioTaskManager)
- 🌐 **Localization warmup** cho performance
- 📦 **Offline package** download
- 🔍 **Health check** với coverage statistics
- 📊 **Analytics logging** chi tiết

---

## 💻 Công nghệ sử dụng

### Backend Stack

```
┌─────────────────────────────────────────────────────────┐
│  .NET 10 • ASP.NET Core Web API & MVC                  │
│  Entity Framework Core 9.0 • Npgsql (PostgreSQL)       │
│  Swashbuckle.AspNetCore (Swagger/OpenAPI)              │
│  Session-based Authentication                           │
│  edge-tts (Python) cho Text-to-Speech                  │
└─────────────────────────────────────────────────────────┘
```

### Mobile Stack

```
┌─────────────────────────────────────────────────────────┐
│  .NET MAUI (net10.0-android / net10.0-ios)             │
│  Mapsui.Maui 5.0 + OpenStreetMap                       │
│  ZXing.Net.Maui 0.4 (QR Scanner)                       │
│  Plugin.Maui.Audio 3.0 (Audio Player)                  │
│  CommunityToolkit.Mvvm 8.3 (MVVM Pattern)              │
│  CommunityToolkit.Maui 9.1 (UI Components)             │
│  sqlite-net-pcl 1.9 (Offline Cache)                    │
└─────────────────────────────────────────────────────────┘
```

### Database & Infrastructure

```
┌─────────────────────────────────────────────────────────┐
│  PostgreSQL (Supabase Cloud)                            │
│  Supabase Storage (Image & Audio hosting)              │
│  SQL Migrations (schema.sql, rls.sql, seed_pois.sql)   │
│  Row Level Security (RLS)                               │
└─────────────────────────────────────────────────────────┘
```

### Development & Deployment

- **IDE**: Visual Studio 2022 (Windows), VS Code
- **API Testing**: Swagger UI, HTTP files
- **Version Control**: Git
- **CI/CD**: Render.com (Docker deployment)
- **Containerization**: Docker (multi-stage builds)

---

## 🏗️ Kiến trúc hệ thống

### Solution Structure

```
VK_StreetFood/
│
├── 📱 src/Client/
│   └── VK.Mobile/                     # .NET MAUI App
│       ├── Views/                     # XAML pages (MainPage, TourPage)
│       ├── ViewModels/                # MVVM ViewModels
│       ├── Services/                  # ApiService, LocationService, AudioService, GeofenceService
│       ├── Models/                    # DTOs & data models
│       ├── Converters/                # XAML value converters
│       ├── Resources/                 # Images, fonts, strings, styles
│       ├── Platforms/                 # Android/iOS specific code
│       └── VK.Mobile.csproj
│
├── 🔧 src/Server/
│   ├── VK.API/                        # REST API Backend
│   │   ├── Controllers/               # API endpoints
│   │   │   ├── POIController.cs
│   │   │   ├── TouristController.cs
│   │   │   ├── AudioController.cs
│   │   │   ├── TourController.cs
│   │   │   ├── AnalyticsController.cs
│   │   │   ├── OfflineController.cs
│   │   │   ├── LocalizationController.cs
│   │   │   ├── PaymentController.cs
│   │   │   └── AdminController.cs
│   │   ├── Services/                  # Business logic
│   │   │   ├── TtsGenerationService.cs    # edge-tts wrapper
│   │   │   ├── AudioTaskManager.cs        # Task deduplication
│   │   │   ├── TouristAppService.cs
│   │   │   └── AnalyticsAppService.cs
│   │   ├── wwwroot/                   # Static files
│   │   │   └── audio/                 # Generated audio files
│   │   ├── App_Data/                  # Application data
│   │   └── VK.API.csproj
│   │
│   ├── VK.Web/                        # Admin/Owner Portal
│   │   ├── Controllers/               # MVC controllers
│   │   │   ├── HomeController.cs          # Login, dashboard
│   │   │   ├── DashboardController.cs     # Admin dashboard
│   │   │   ├── PoiController.cs           # POI CRUD
│   │   │   ├── TourController.cs          # Tour CRUD
│   │   │   ├── AudioController.cs         # Audio management
│   │   │   ├── TranslationController.cs   # i18n management
│   │   │   ├── OwnerController.cs         # Owner functions
│   │   │   ├── OwnerRegistrationController.cs
│   │   │   ├── OwnerContentApprovalController.cs
│   │   │   ├── PaymentController.cs       # Payment config
│   │   │   └── UsageHistoryController.cs
│   │   ├── Views/                     # Razor views
│   │   ├── Services/                  # Web services
│   │   └── VK.Web.csproj
│   │
│   ├── VK.Core/                       # Domain Layer
│   │   └── Entities/                  # Domain entities
│   │       ├── PointOfInterest.cs
│   │       ├── PointOfInterestTranslation.cs
│   │       ├── AudioContent.cs
│   │       ├── Tour.cs
│   │       ├── TourTranslation.cs
│   │       ├── CategoryAndTags.cs
│   │       ├── Tourist.cs
│   │       ├── VisitLog.cs
│   │       ├── Analytics.cs
│   │       ├── User.cs
│   │       ├── Vendor.cs
│   │       ├── PoiContentChangeRequest.cs
│   │       └── QrPaymentConfig.cs
│   │
│   └── VK.Infrastructure/             # Data Access Layer
│       ├── Data/
│       │   ├── ApplicationDbContext.cs
│       │   └── Configurations/        # EF Core entity configs
│       └── Repositories/              # Repository pattern
│
├── 🔄 src/Shared/
│   ├── VK.Contracts/                  # API Contracts (DTOs)
│   │   ├── Requests/                  # Request models
│   │   └── Responses/                 # Response models
│   └── VK.Shared/                     # Shared utilities
│       ├── Constants/                 # App constants
│       └── DTOs/                      # Shared DTOs
│
├── 🗄️ supabase/
│   ├── migrations/                    # SQL migration scripts
│   │   └── delta_align_existing_db.sql
│   ├── schema.sql                     # Database schema
│   ├── rls.sql                        # Row Level Security policies
│   ├── seed_pois.sql                  # Seed data
│   └── MIGRATION_INSTRUCTIONS.md      # Migration guide
│
├── 🧪 tests/
│   ├── VK.API.Tests/
│   │   ├── Unit/                      # Unit tests
│   │   │   ├── TouristAppServiceTests.cs
│   │   │   └── AnalyticsAppServiceTests.cs
│   │   ├── Integration/               # Integration tests
│   │   │   ├── TouristEndpointsTests.cs
│   │   │   └── AnalyticsEndpointsTests.cs
│   │   └── Infrastructure/
│   │       └── CustomWebApplicationFactory.cs
│   └── VK.Core.Tests/
│
├── 🐳 Docker/
│   ├── Dockerfile.api                 # API container definition
│   ├── Dockerfile.web                 # Web container definition
│   └── render.yaml                    # Render.com deployment config
│
├── 🖼️ images/
│   ├── poi/                           # POI images
│   └── backgroundad/                  # Background ads
│
├── 📄 docs/
│   └── prd.docx                       # Product Requirements Document
│
├── VKStreetFood.slnx                  # Solution file
└── README.md

```

### Architecture Diagram

```
┌────────────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │ Mobile MAUI  │  │  Admin Web   │  │  Owner Web   │        │
│  │ (Tourists)   │  │  (ASP.NET)   │  │  (ASP.NET)   │        │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘        │
└─────────┼──────────────────┼──────────────────┼────────────────┘
          │                  │                  │
          │      HTTPS REST + Session Cookie Auth
          ▼                  ▼                  ▼
┌────────────────────────────────────────────────────────────────┐
│                      APPLICATION LAYER                         │
│         ┌────────────────────────────────────────┐             │
│         │   VK.API (Web API) + VK.Web (MVC)     │             │
│         │   ┌────────────────────────────┐      │             │
│         │   │  Controllers               │      │             │
│         │   ├────────────────────────────┤      │             │
│         │   │  Application Services      │      │             │
│         │   │  ├─ TouristAppService      │      │             │
│         │   │  ├─ AnalyticsAppService    │      │             │
│         │   │  ├─ TtsGenerationService   │      │             │
│         │   │  └─ AudioTaskManager       │      │             │
│         │   └────────────┬───────────────┘      │             │
│         └────────────────┼────────────────────────┘             │
└──────────────────────────┼──────────────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────────────┐
│                       DOMAIN LAYER                             │
│         ┌────────────────────────────────────────┐             │
│         │   VK.Core (Domain Entities)           │             │
│         │   ├─ PointOfInterest                  │             │
│         │   ├─ Tour, Tourist                    │             │
│         │   ├─ AudioContent                     │             │
│         │   ├─ Analytics, VisitLog              │             │
│         │   └─ User, Vendor                     │             │
│         └────────────────────────────────────────┘             │
└────────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER                         │
│         ┌────────────────────────────────────────┐             │
│         │   VK.Infrastructure                   │             │
│         │   ├─ ApplicationDbContext (EF Core)   │             │
│         │   ├─ Repositories                     │             │
│         │   └─ Configurations                   │             │
│         └────────────┬───────────────────────────┘             │
└──────────────────────┼──────────────────────────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────────────────────────┐
│                    DATA PERSISTENCE                            │
│  ┌──────────────────────┐    ┌──────────────────────┐         │
│  │ PostgreSQL (Supabase)│    │ Supabase Storage     │         │
│  │ - Tables             │    │ - Images (poi-images)│         │
│  │ - RLS Policies       │    │ - Audio files        │         │
│  └──────────────────────┘    └──────────────────────┘         │
└────────────────────────────────────────────────────────────────┘
```

### Database Schema (Key Tables)

```sql
-- Core POI
PointsOfInterest (Id, Name, Latitude, Longitude, GeofenceRadius, CategoryId, ...)
PointOfInterestTranslations (Id, PoiId, LanguageCode, Name, Description, ...)
AudioContents (Id, PoiId, LanguageCode, AudioUrl, ...)

-- Tours
Tours (Id, Name, TourType, EstimatedDurationMinutes, ...)
TourTranslations (Id, TourId, LanguageCode, Name, Description, ...)
TourPointsOfInterest (TourId, PoiId, OrderIndex, ...)

-- Categories & Tags
Categories (Id, Name, IconUrl, ...)
Tags (Id, Name, ...)
PointOfInterestTag (PoiId, TagId)

-- Tourists & Analytics
Tourists (Id, DeviceId, PreferredLanguage, ...)
VisitLogs (Id, TouristId, PoiId, VisitedAt, ...)
Analytics (Id, EventType, EntityId, EntityType, Timestamp, ...)

-- Vendors & Users
Vendors (Id, Name, ContactInfo, OpeningHours, ...)
Users (Id, Email, PasswordHash, Role, ...)
PoiOwnerRegistrations (Id, UserId, PoiId, Status, ...)
PoiContentChangeRequests (Id, PoiId, RequesterId, Status, ...)

-- Payment
QrPaymentConfigs (Id, PoiId, PaymentMethod, QrCodeData, ...)
```

---

## 📦 Cài đặt

### Yêu cầu hệ thống

#### Development Machine
- **OS**: Windows 10/11, macOS 12+, hoặc Linux
- **IDE**: Visual Studio 2022 (17.8+) hoặc VS Code
- **.NET SDK**: .NET 10.0 SDK
- **Database**: PostgreSQL client (hoặc dùng Supabase web interface)
- **Mobile Development**:
  - Android: Android SDK API 21+
  - iOS: Xcode 15+ (chỉ trên macOS)

#### Runtime Requirements
- **Backend**: .NET 10.0 Runtime, Python 3.x (cho edge-tts)
- **Mobile**: Android 5.0+ (API 21+) hoặc iOS 15.0+

### Clone Repository

```bash
git clone https://github.com/your-username/VK_StreetFood.git
cd VK_StreetFood
```

### Setup Database (Supabase)

1. Tạo project trên [Supabase](https://supabase.com/)
2. Copy connection string từ Settings > Database
3. Chạy migrations theo thứ tự:

```bash
# Trong Supabase SQL Editor
# 1. Tạo schema
psql -f supabase/schema.sql

# 2. Áp dụng RLS policies
psql -f supabase/rls.sql

# 3. Seed dữ liệu mẫu
psql -f supabase/seed_pois.sql

# 4. (Optional) Chạy delta migration nếu DB đã tồn tại
psql -f supabase/migrations/delta_align_existing_db.sql
```

Hoặc copy-paste trực tiếp từng file vào Supabase SQL Editor.

### Install Dependencies

#### Backend (.NET)

```bash
# Restore NuGet packages
dotnet restore VKStreetFood.slnx
```

#### Mobile (MAUI)

```bash
# Workload đã được cài trong Visual Studio Installer
# Nếu dùng CLI, cài workload:
dotnet workload install maui
```

---

## ⚙️ Cấu hình

### 1. API Configuration

Tạo `src/Server/VK.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-supabase-host.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "TtsGenerationService": {
    "PythonExecutable": "python",
    "OutputDirectory": "wwwroot/audio",
    "DefaultVoice": "vi-VN-HoaiMyNeural"
  }
}
```

### 2. Web Portal Configuration

Tạo `src/Server/VK.Web/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-supabase-host.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
  },
  "ApiSettings": {
    "BaseUrl": "http://localhost:5089/api/"
  },
  "AdminAuth": {
    "Email": "admin@vkstreetfood.com",
    "Password": "Admin@123"
  },
  "SupabaseStorage": {
    "Url": "https://your-project.supabase.co",
    "ServiceRoleKey": "your-service-role-key",
    "Bucket": "poi-images",
    "PublicBaseUrl": "https://your-project.supabase.co/storage/v1/object/public/poi-images/"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 3. Mobile App Configuration

Sửa `src/Client/VK.Mobile/Services/ApiService.cs`:

```csharp
// Development
private const string BaseUrl = "http://10.0.2.2:5089/api/"; // Android Emulator
// private const string BaseUrl = "http://localhost:5089/api/"; // iOS Simulator
// private const string BaseUrl = "http://192.168.1.100:5089/api/"; // Physical device
```

### 4. Install edge-tts (cho TTS)

```bash
# Global installation
pip install edge-tts

# Hoặc trong virtual environment
python -m venv .venv
source .venv/bin/activate  # Linux/macOS
# .venv\Scripts\activate  # Windows
pip install edge-tts
```

---

## 🚀 Chạy ứng dụng

### Development Mode

#### 1. Chạy API Backend

```bash
cd src/Server/VK.API
dotnet run
```

API sẽ chạy tại: `http://localhost:5089`  
Swagger UI: `http://localhost:5089/swagger`

#### 2. Chạy Web Portal

```bash
cd src/Server/VK.Web
dotnet run
```

Web portal sẽ chạy tại: `http://localhost:5173`

**Login credentials (default admin):**
- Email: `admin@vkstreetfood.com`
- Password: `Admin@123`

#### 3. Chạy Mobile App

**Trong Visual Studio:**
1. Set `VK.Mobile` là startup project
2. Chọn target (Android Emulator hoặc iOS Simulator)
3. Nhấn F5 để debug

**Hoặc dùng CLI:**

```bash
cd src/Client/VK.Mobile

# Android
dotnet build -t:Run -f net10.0-android

# iOS (chỉ trên macOS)
dotnet build -t:Run -f net10.0-ios
```

### Production Build

#### API

```bash
cd src/Server/VK.API
dotnet publish -c Release -o ./publish
```

#### Web

```bash
cd src/Server/VK.Web
dotnet publish -c Release -o ./publish
```

#### Mobile

```bash
# Android APK
cd src/Client/VK.Mobile
dotnet publish -f net10.0-android -c Release

# iOS (macOS only)
dotnet publish -f net10.0-ios -c Release
```

---

## 🧪 Testing

### Run Unit Tests

```bash
# Chạy tất cả tests
dotnet test

# Chạy tests với coverage
dotnet test --collect:"XPlat Code Coverage"

# Chạy tests trong một project cụ thể
dotnet test tests/VK.API.Tests/VK.API.Tests.csproj
```

### Test Coverage

```bash
# Cài đặt ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate coverage report
reportgenerator \
  -reports:tests/*/TestResults/*/coverage.cobertura.xml \
  -targetdir:coverage-report \
  -reporttypes:Html

# Mở report
open coverage-report/index.html
```

### Integration Tests

Integration tests sử dụng `CustomWebApplicationFactory` để test API endpoints với in-memory database.

```bash
cd tests/VK.API.Tests
dotnet test --filter "FullyQualifiedName~Integration"
```

---

## 📦 Deployment

### Docker Deployment (Render.com)

Project đã được cấu hình sẵn cho deployment lên Render.com với Docker.

#### Prerequisites
1. Tài khoản Render.com
2. Repository đã push lên GitHub
3. Supabase database đã setup

#### Deploy Steps

1. **Connect GitHub repo** trên Render.com
2. **Auto-deploy** sẽ trigger từ `render.yaml`
3. **Configure Environment Variables** trên Render Dashboard:

**VK-API Service:**
```
ConnectionStrings__DefaultConnection=<your-supabase-connection-string>
ASPNETCORE_ENVIRONMENT=Production
```

**VK-Web Service:**
```
ConnectionStrings__DefaultConnection=<your-supabase-connection-string>
ApiSettings__BaseUrl=<your-api-url>/api/
AdminAuth__Email=admin@vkstreetfood.com
AdminAuth__Password=<secure-password>
SupabaseStorage__Url=<your-supabase-url>
SupabaseStorage__ServiceRoleKey=<your-service-role-key>
SupabaseStorage__PublicBaseUrl=<your-supabase-storage-url>
ASPNETCORE_ENVIRONMENT=Production
```

4. **Deploy** - Render sẽ tự động:
   - Build Docker images từ `Dockerfile.api` và `Dockerfile.web`
   - Deploy containers
   - Expose services qua HTTPS

#### Docker Build Local

```bash
# Build API image
docker build -f Dockerfile.api -t vk-api .

# Build Web image
docker build -f Dockerfile.web -t vk-web .

# Run locally
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="<connection-string>" \
  vk-api
```

### Manual Deployment (Linux Server)

```bash
# 1. Install .NET 10 runtime
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 10.0

# 2. Install Python & edge-tts
sudo apt install python3 python3-pip
pip3 install edge-tts

# 3. Deploy API
cd /var/www/vk-api
dotnet VK.API.dll

# 4. Setup systemd service
sudo systemctl enable vk-api
sudo systemctl start vk-api

# 5. Setup Nginx reverse proxy
# (Cấu hình Nginx để proxy từ port 80/443 -> 5089)
```

### Mobile App Distribution

#### Android
1. Build Release APK: `dotnet publish -f net10.0-android -c Release`
2. Sign APK với keystore
3. Upload lên Google Play Console

#### iOS
1. Build Archive trong Xcode
2. Submit to App Store Connect
3. TestFlight beta distribution

---

## 🔧 Troubleshooting

### 1. API không kết nối được từ Mobile

**Triệu chứng:** `Unable to connect to the remote server`

**Giải pháp:**
- ✅ Android Emulator: Dùng `http://10.0.2.2:5089/api/`
- ✅ iOS Simulator: Dùng `http://localhost:5089/api/`
- ✅ Physical Device: Dùng IP máy (VD: `http://192.168.1.100:5089/api/`)
- ✅ Kiểm tra firewall cho phép port 5089
- ✅ API phải đang chạy trước khi test mobile

### 2. Database Migration Failed

**Triệu chứng:** `Table 'PointsOfInterest' does not exist`

**Giải pháp:**
1. Verify connection string
2. Chạy migrations theo đúng thứ tự: `schema.sql` → `rls.sql` → `seed_pois.sql`
3. Check logs trong Supabase Table Editor

### 3. Audio không phát

**Triệu chứng:** Geofence trigger nhưng không có âm thanh

**Giải pháp:**
- ✅ Kiểm tra `AudioContents` table có records cho POI + language
- ✅ Verify `AudioUrl` đúng path trong `wwwroot/audio/`
- ✅ File MP3 phải tồn tại và không corrupt
- ✅ Check permissions đọc file

```sql
-- Verify audio coverage
SELECT poi_id, language_code, audio_url
FROM "AudioContents"
WHERE poi_id = 'your-poi-id';
```

### 4. TTS Generation Failed

**Triệu chứng:** `edge-tts command not found`

**Giải pháp:**
```bash
# Verify edge-tts installed
edge-tts --list-voices

# Re-install nếu cần
pip install --upgrade edge-tts

# Check Python path trong appsettings.json
"TtsGenerationService": {
  "PythonExecutable": "python3"  # hoặc đường dẫn đầy đủ
}
```

### 5. Geofence không trigger

**Triệu chứng:** Đã vào vùng POI nhưng không callback

**Giải pháp:**
- ✅ Kiểm tra GPS permissions đã grant
- ✅ Verify `GeofenceRadius` trong DB (thử tăng lên 100m để test)
- ✅ Location service đang bật trên device
- ✅ Debug distance calculation trong `GeofenceService.cs`

### 6. Build Mobile Failed

**Triệu chứng:** `Android SDK not found`

**Giải pháp:**
1. Mở Visual Studio Installer
2. Modify → ☑️ **Mobile development with .NET**
3. Install Android SDK (API 21+)
4. Restart Visual Studio

### 7. Owner không login được

**Triệu chứng:** Login failed với correct credentials

**Giải pháp:**
```sql
-- Check owner status
SELECT "Email", "Status", "IsVerified"
FROM "Users"
WHERE "Email" = 'owner@example.com';

-- Approve owner
UPDATE "Users"
SET "Status" = 'Approved', "IsVerified" = true
WHERE "Email" = 'owner@example.com';
```

### 8. Supabase Storage Upload Failed

**Triệu chứng:** `Unable to upload image to Supabase Storage`

**Giải pháp:**
- ✅ Verify `ServiceRoleKey` đúng
- ✅ Bucket `poi-images` đã tạo và public
- ✅ Check RLS policies cho bucket
- ✅ File size < 5MB

---

## 🗺️ Roadmap

### ✅ Phase 1: MVP (Completed)
- [x] Core API endpoints (POI, Tourist, Audio, Tour, Analytics)
- [x] Mobile MAUI app với geofence & map
- [x] Web admin portal với dashboard
- [x] Audio guide đa ngôn ngữ (vi/en/ko)
- [x] Offline support (SQLite cache, audio warmup)
- [x] Owner registration & approval workflow
- [x] QR code scanner & payment integration
- [x] TTS on-demand generation với edge-tts
- [x] Docker deployment (Render.com)

### 🚧 Phase 2: Enhancement (In Progress)
- [ ] **Testing**: Tăng coverage lên 80%+
- [ ] **Analytics**: Heatmap visualization, user journey tracking
- [ ] **Performance**: Redis caching layer, CDN for audio
- [ ] **Localization**: Thêm tiếng Nhật, Trung, Pháp
- [ ] **Mobile**: Push notifications cho tour suggestions

### 📅 Phase 3: Advanced Features (Planned)
- [ ] **AI Features**:
  - [ ] Gợi ý POI dựa trên ML (user preferences)
  - [ ] AR navigation overlay
  - [ ] Voice-controlled tour guide
  - [ ] Chatbot hỗ trợ du khách
- [ ] **Social Features**:
  - [ ] User reviews & ratings system
  - [ ] Share experiences lên social media
  - [ ] Leaderboard & gamification
  - [ ] Friend recommendations
- [ ] **Business Features**:
  - [ ] Owner analytics dashboard
  - [ ] Promotion & ads management
  - [ ] Table booking integration
  - [ ] Loyalty rewards program

### 🔮 Phase 4: Scale (Future)
- [ ] **Multi-region**: Mở rộng sang khu phố khác (Bùi Viện, Bến Thành, Chợ Lớn)
- [ ] **IoT Integration**: Beacon-based micro-location
- [ ] **API Marketplace**: Public API cho 3rd party developers
- [ ] **White-label**: Deploy cho các khu phố khác

---

## 🤝 Đóng góp

Mọi đóng góp đều được hoan nghênh! 🎉

### Quy trình Contribute

1. **Fork** repository
2. **Clone** về máy: `git clone https://github.com/your-username/VK_StreetFood.git`
3. **Create** feature branch: `git checkout -b feature/amazing-feature`
4. **Commit** changes: `git commit -m "feat(api): add amazing feature"`
5. **Push** to branch: `git push origin feature/amazing-feature`
6. **Open** Pull Request

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
public async Task<List<PointOfInterest>> GetNearbyPoisAsync(
    double latitude,
    double longitude,
    double radiusMeters)
{
    // Implementation
}
```

#### SQL
- Use **PascalCase** cho table & column names (EF Core convention)
- Add indexes cho foreign keys
- Always include `CreatedAt`, `UpdatedAt` timestamps

#### Git Commit Messages

Format: `<type>(<scope>): <subject>`

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Code formatting
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Build/dependencies

**Examples:**
```
feat(api): add geofence radius configuration endpoint
fix(mobile): resolve audio playback crash on iOS 16
docs(readme): update deployment instructions
test(api): add integration tests for tourist endpoints
```

### Areas to Contribute

🐛 **Bug Fixes**: Check [Issues](https://github.com/your-username/VK_StreetFood/issues)  
✨ **Features**: See [Roadmap](#roadmap)  
📖 **Documentation**: Improve README, add code comments  
🧪 **Testing**: Write unit/integration tests  
🌐 **Localization**: Add language support (ja, zh, fr)  
🎨 **UI/UX**: Improve mobile app design

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **OpenStreetMap** - Dữ liệu bản đồ mở
- **Mapsui** - Cross-platform map rendering engine
- **Supabase** - PostgreSQL hosting & storage
- **Microsoft** - .NET MAUI framework
- **edge-tts** - Free TTS API từ Microsoft Edge
- **Render.com** - Free Docker hosting
- Cộng đồng **Phố Vĩnh Khánh** đã hỗ trợ thu thập dữ liệu

---

## 📞 Contact

- **Email**: leviethoangthien2005@gmail.com
- **GitHub**: [https://github.com/Hoangg-Thien/VK_StreetFood](https://github.com/your-username/VK_StreetFood)

---

<div align="center">

[⬆ Back to top](#vk-streetfood---nền-tảng-du-lịch-ẩm-thực-phố-vĩnh-khánh)

</div>
