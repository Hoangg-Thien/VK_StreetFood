# VK Street Food Mobile App 📱

## Tech Stack

- **.NET MAUI** - Cross-platform framework
- **OpenStreetMap** via Mapsui.Maui - Maps
- **ZXing.Net.Maui** - QR Code Scanner
- **Plugin.Maui.Audio** - Audio Player
- **CommunityToolkit.Mvvm** - MVVM Pattern
- **CommunityToolkit.Maui** - UI Components

## Features

- ✅ Interactive map with OpenStreetMap
- ✅ Display POI markers (12 food stalls)
- ✅ Real-time GPS tracking
- ✅ Geofencing (auto-trigger audio within 50m)
- ✅ QR Code scanner
- ✅ Multi-language audio guide (vi/en/ko)
- ✅ POI details with ratings & favorites
- ✅ Visit history tracking
- ✅ Background location updates

## Prerequisites

1. **Visual Studio 2022** (v17.8+) with MAUI workload
2. **Android SDK** (API 21+) hoặc **iOS SDK**
3. **Backend API running** at `http://localhost:5089`

## Setup & Run

### 1️⃣ Restore Packages

```powershell
cd d:\VK_StreetFood\src\Client\VK.Mobile
dotnet restore
```

### 2️⃣ Update API URL (if needed)

Edit `Models/AppSettings.cs`:

```csharp
public const string ApiBaseUrl = "http://YOUR_IP:5089/api/";  // Change to your IP
```

> **Note**: Nếu test trên Android Emulator, dùng `http://10.0.2.2:5089` thay vì `localhost`

### 3️⃣ Start Backend API First

```powershell
cd d:\VK_StreetFood
dotnet run --project src/Server/VK.API/VK.API.csproj
```

### 4️⃣ Run Mobile App

#### Option A: Visual Studio

1. Open `VKStreetFood.slnx` in Visual Studio
2. Set `VK.Mobile` as startup project
3. Select target:
   - **Android Emulator** (Pixel 5 API 34+)
   - **Windows Machine** (for development)
   - **Physical Device** (enable Developer Mode)
4. Press **F5** to run

#### Option B: Command Line

```powershell
# Android
dotnet build -t:Run -f net10.0-android

# Windows
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

## Testing Checklist

### ✅ Basic Navigation

- [ ] App opens with map view
- [ ] 12 POI markers visible on map
- [ ] Can zoom in/out, pan map
- [ ] Click marker → Navigate to POI detail

### ✅ GPS & Location

- [ ] App requests location permission
- [ ] Blue dot shows current location
- [ ] Location updates every 5 seconds
- [ ] "Tracking" badge shows green

### ✅ Geofencing

- [ ] Walk within 50m of POI → Alert appears
- [ ] Audio auto-plays (if available)
- [ ] Visit logged to history

### ✅ QR Scanner

- [ ] Click QR button → Camera opens
- [ ] Scan QR code (use test code: `VK-OC-OANH`)
- [ ] Navigates to POI detail page
- [ ] Visit logged as "qr_scan"

### ✅ POI Detail Page

- [ ] POI name, image, description display
- [ ] Address shown with 📍 icon
- [ ] Audio player controls work (Play/Pause/Stop)
- [ ] Can switch language (vi/en/ko)
- [ ] Heart icon toggles favorite
- [ ] Star rating buttons work
- [ ] Vendor list displayed

### ✅ Audio Playback

- [ ] Click Play → Audio downloads & plays
- [ ] Pause button works
- [ ] Stop button stops & resets
- [ ] Playback complete tracked to analytics

## Troubleshooting

### ❌ "Cannot connect to API"

**Solution**:

- Ensure backend is running on `http://localhost:5089`
- If testing on emulator/device, use your computer's IP:
  ```csharp
  // In AppSettings.cs
  public const string ApiBaseUrl = "http://192.168.1.XXX:5089/api/";
  ```
- Check firewall allows port 5089

### ❌ "Location permission denied"

**Solution**:

- Android: Settings → Apps → VK.Mobile → Permissions → Location → Allow
- Windows: Settings → Privacy → Location → Allow apps to access location

### ❌ "Camera not working"

**Solution**:

- Android: Settings → Apps → VK.Mobile → Permissions → Camera → Allow
- Restart app after granting permission

### ❌ Map not loading

**Solution**:

- Check internet connection (OpenStreetMap requires internet)
- Clear app data and restart

### ❌ Audio not playing

**Solution**:

- Verify audio files exist on backend: `http://localhost:5089/audio/`
- Check audio URL in POI response
- Try playing from browser first

## Test Data

### Test QR Codes

- `VK-OC-OANH` - Ốc Oanh (Michelin)
- `VK-OC-SAU-NO` - Ốc Sấu Nổ
- `VK-BOT-MY-TRANG` - Bột Mì Trang
- `VK-LAU-DE-THUY` - Lẩu Dê Thúy
- `VK-BANH-MI-THIT` - Bánh Mì Hai Đệ

### Test Coordinates (Vĩnh Khánh)

- Latitude: `10.761`
- Longitude: `106.703`

## Project Structure

```
VK.Mobile/
├── Models/          # Data models
├── Services/        # API, Location, Audio services
├── ViewModels/      # MVVM ViewModels
├── Views/           # XAML Pages
├── Converters/      # Value converters for binding
├── Platforms/       # Platform-specific code
│   ├── Android/     # Android permissions & config
│   └── iOS/         # iOS config
└── Resources/       # Images, fonts, styles
```

## Known Issues

- ⚠️ Background location tracking works only when app is in foreground (background service not implemented yet)
- ⚠️ Audio caching not implemented (downloads each time)
- ⚠️ Map performance may be slow with many POIs (>50)

## Next Steps

1. Implement background location service
2. Add offline map caching
3. Implement push notifications
4. Add tour routes feature
5. Social sharing

---
