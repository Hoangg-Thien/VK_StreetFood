using Microsoft.Extensions.DependencyInjection;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using BruTile.Cache;
using BruTile.Predefined;
using VK.Mobile.ViewModels;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.Views;

public partial class MainMapPage : ContentPage
{
    private readonly MainMapViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private MapControl? _mapControl;
    private WritableLayer? _poiLayer;
    private WritableLayer? _locationLayer;
    private bool _hasCenteredOnUser = false;
    // Viewport save/restore on tab switch
    private double _savedCenterX = double.NaN;
    private double _savedCenterY = double.NaN;
    private double _savedResolution = double.NaN;

    // OSM resolution for zoom level: 156543.03392804062 / 2^z
    private static double ZoomResolution(int level) =>
        156543.03392804062 / Math.Pow(2, level);

    private static bool IsOfflineMode => Connectivity.NetworkAccess != NetworkAccess.Internet;

    public MainMapPage(MainMapViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        BindingContext = _viewModel;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            InitializeMap();

            // Wire up collection / property changes TRƯỚC KHI load data
            // để đảm bảo mọi thay đổi đều trigger render trên map
            _viewModel.Pois.CollectionChanged += (_, _) => UpdatePOIMarkers();
            _viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.CurrentLocation))
                    UpdateCurrentLocationMarker();
                if (args.PropertyName == nameof(_viewModel.NearestPoi))
                    UpdatePOIMarkers(); // re-draw để highlight POI gần nhất
            };

            // Geofence tự động mở NowPlayingPage
            _viewModel.GeofencePOITriggered += OnGeofencePOITriggered;

            try { await _viewModel.InitializeCommand.ExecuteAsync(null); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"VM init error: {ex}"); }

            // Draw POIs + location ngay sau init (phòng trường hợp event không fire)
            UpdatePOIMarkers();
            UpdateCurrentLocationMarker();

            System.Diagnostics.Debug.WriteLine($"After init: {_viewModel.Pois.Count} POIs, Location={_viewModel.CurrentLocation?.Latitude},{_viewModel.CurrentLocation?.Longitude}");

            // Auto-start tracking
            if (!_viewModel.IsTracking)
            {
                try { await _viewModel.StartTrackingCommand.ExecuteAsync(null); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Tracking error: {ex}"); }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnPageLoaded error: {ex}");
        }
    }

    private void InitializeMap()
    {
        try
        {
            _mapControl = new MapControl();
            var map = new Mapsui.Map();

            // Tile layer với FileCache để lưu tile xuống disk.
            // Khi online: fetch từ OSM và cache. Khi offline: dùng tile đã cache.
            map.Layers.Add(CreateCachedOsmTileLayer());
            if (IsOfflineMode)
                System.Diagnostics.Debug.WriteLine("Map offline mode: using cached OSM tiles");

            // POI markers layer
            _poiLayer = new WritableLayer { Name = "POIs", Style = null };
            map.Layers.Add(_poiLayer);

            // User location layer
            _locationLayer = new WritableLayer { Name = "Location", Style = null };
            map.Layers.Add(_locationLayer);

            _mapControl.Map = map;
            _mapControl.SizeChanged += OnMapControlSizeChanged;
            _mapControl.Info += MapControl_Info;

            MapContainer.Content = _mapControl;
            System.Diagnostics.Debug.WriteLine("Map initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeMap error: {ex}");
            MapContainer.Content = new Label
            {
                Text = $"Map load failed: {ex.Message}",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Colors.Red
            };
        }
    }

    private static TileLayer CreateCachedOsmTileLayer()
    {
        try
        {
            var tileCacheDir = Path.Combine(FileSystem.CacheDirectory, "osm_tiles");
            Directory.CreateDirectory(tileCacheDir);
            var fileCache = new FileCache(tileCacheDir, "png", TimeSpan.FromDays(30));
            var tileSource = KnownTileSources.Create(KnownTileSource.OpenStreetMap, persistentCache: fileCache);
            return new TileLayer(tileSource);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] FileCache init failed, using default: {ex.Message}");
            return OpenStreetMap.CreateTileLayer();
        }
    }

    private void OnMapControlSizeChanged(object? sender, EventArgs e)
    {
        if (_mapControl == null || _mapControl.Width <= 0 || _mapControl.Height <= 0)
            return;

        // Only fire once
        _mapControl.SizeChanged -= OnMapControlSizeChanged;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var lat = _viewModel.CurrentLocation?.Latitude ?? AppSettings.DefaultLatitude;
                var lon = _viewModel.CurrentLocation?.Longitude ?? AppSettings.DefaultLongitude;
                var center = SphericalMercator.FromLonLat(lon, lat).ToMPoint();
                var resolution = ZoomResolution(AppSettings.DefaultZoomLevel);

                _mapControl.Map.Navigator.CenterOnAndZoomTo(center, resolution);
                _hasCenteredOnUser = _viewModel.CurrentLocation != null;

                System.Diagnostics.Debug.WriteLine($"Map centered on ({lat:F4}, {lon:F4}) res={resolution:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigator error: {ex}");
            }
        });
    }

    private void UpdatePOIMarkers()
    {
        if (_poiLayer == null || _mapControl?.Map == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _poiLayer.Clear();

                System.Diagnostics.Debug.WriteLine($"Drawing {_viewModel.Pois.Count} POI markers on map");

                foreach (var poi in _viewModel.Pois)
                {
                    // Bỏ qua POIs không có tọa độ hợp lệ
                    if (poi.Latitude == 0 && poi.Longitude == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping POI '{poi.Name}' - no coordinates (Id={poi.Id})");
                        continue;
                    }

                    var point = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                    var feature = new PointFeature(point.ToMPoint());

                    feature["poi_id"] = poi.Id;
                    feature["poi_name"] = poi.Name;

                    bool isNearest = _viewModel.NearestPoi?.Id == poi.Id;

                    if (isNearest)
                    {
                        // Vòng sáng ngoài (glow ring) cho POI gần nhất
                        feature.Styles.Add(new SymbolStyle
                        {
                            SymbolScale = 1.6,
                            Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(255, 107, 53, 60)), // cam nhạt
                            Outline = new Pen(Mapsui.Styles.Color.FromString("#FF6B35"), 2),
                            SymbolType = SymbolType.Ellipse
                        });
                        // Điểm chính lớn hơn + màu orange đậm
                        feature.Styles.Add(new SymbolStyle
                        {
                            SymbolScale = 1.0,
                            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#FF6B35")),
                            Outline = new Pen(Mapsui.Styles.Color.White, 3),
                            SymbolType = SymbolType.Ellipse
                        });
                    }
                    else
                    {
                        // Marker bình thường
                        feature.Styles.Add(new SymbolStyle
                        {
                            SymbolScale = 0.6,
                            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#FF5722")),
                            Outline = new Pen(Mapsui.Styles.Color.White, 2),
                            SymbolType = SymbolType.Ellipse
                        });
                    }

                    // Label below marker
                    feature.Styles.Add(new LabelStyle
                    {
                        Text = poi.Name,
                        ForeColor = isNearest
                            ? Mapsui.Styles.Color.FromString("#FF6B35")
                            : Mapsui.Styles.Color.FromString("#333333"),
                        BackColor = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(255, 255, 255, 200)),
                        Font = new Mapsui.Styles.Font { Size = isNearest ? 12 : 10 },
                        HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                        Offset = new Offset(0, -20)
                    });

                    _poiLayer.Add(feature);
                }

                _mapControl.Map.Refresh();
                System.Diagnostics.Debug.WriteLine($"Total POI markers drawn: {_poiLayer.GetFeatures().Count()}");

                // Nếu chưa center on user, zoom to fit tất cả POIs
                if (!_hasCenteredOnUser && _viewModel.Pois.Count > 0)
                {
                    var extent = _poiLayer.Extent;
                    if (extent != null)
                    {
                        // Zoom vào khu vực POIs với padding
                        _mapControl.Map.Navigator.ZoomToBox(extent, MBoxFit.Fit, 50);
                        System.Diagnostics.Debug.WriteLine("Map zoomed to fit all POIs");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePOIMarkers error: {ex}");
            }
        });
    }

    private void UpdateCurrentLocationMarker()
    {
        if (_locationLayer == null || _mapControl?.Map == null || _viewModel.CurrentLocation == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var lat = _viewModel.CurrentLocation.Latitude;
                var lon = _viewModel.CurrentLocation.Longitude;

                System.Diagnostics.Debug.WriteLine($"Updating user location marker: ({lat:F6}, {lon:F6})");

                _locationLayer.Clear();

                var point = SphericalMercator.FromLonLat(lon, lat);
                var mpoint = point.ToMPoint();

                // Accuracy ring (outer, semi-transparent blue)
                var outerFeature = new PointFeature(mpoint);
                outerFeature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.5,
                    Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(33, 150, 243, 50)),
                    Outline = new Pen(new Mapsui.Styles.Color(33, 150, 243, 100), 2),
                    SymbolType = SymbolType.Ellipse
                });
                _locationLayer.Add(outerFeature);

                // Blue dot (Google Maps style)
                var dotFeature = new PointFeature(mpoint);
                dotFeature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.5,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#2196F3")),
                    Outline = new Pen(Mapsui.Styles.Color.White, 3),
                    SymbolType = SymbolType.Ellipse
                });
                _locationLayer.Add(dotFeature);

                _mapControl.Map.Refresh();

                // Center on user first time location is received
                if (!_hasCenteredOnUser)
                {
                    _hasCenteredOnUser = true;
                    var res = ZoomResolution(17);
                    _mapControl.Map.Navigator.CenterOnAndZoomTo(mpoint, res);
                    System.Diagnostics.Debug.WriteLine($"Map centered on user at ({lat:F6}, {lon:F6})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateLocationMarker error: {ex}");
            }
        });
    }

    private async void MapControl_Info(object? sender, MapInfoEventArgs e)
    {
        if (_poiLayer == null || _mapControl == null) return;

        try
        {
            var screenPos = e.ScreenPosition;
            var worldPos = _mapControl.Map.Navigator.Viewport.ScreenToWorld(screenPos.X, screenPos.Y);

            PointFeature? closest = null;
            double minDist = double.MaxValue;

            foreach (var f in _poiLayer.GetFeatures())
            {
                if (f is PointFeature pf)
                {
                    var dx = pf.Point.X - worldPos.X;
                    var dy = pf.Point.Y - worldPos.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 300 && dist < minDist)
                    {
                        minDist = dist;
                        closest = pf;
                    }
                }
            }

            if (closest?["poi_id"] is int closestId)
            {
                var poi = _viewModel.Pois.FirstOrDefault(p => p.Id == closestId);
                if (poi != null)
                {
                    // Show popup with audio test option
                    var L = LocalizationResourceManager.Instance;
                    var listen = L["POIActionListen"];
                    var detail = L["POIActionDetail"];
                    var action = await DisplayActionSheet(
                        poi.Name,
                        L["POIActionClose"],
                        null,
                        listen,
                        detail);

                    if (action == listen)
                    {
                        await _viewModel.TestAudioCommand.ExecuteAsync(poi);
                    }
                    else if (action == detail)
                    {
                        await _viewModel.POISelectedCommand.ExecuteAsync(poi);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapControl_Info error: {ex}");
        }
    }

    private async void OnLanguageChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var langCode = AppSettings.SupportedLanguages[picker.SelectedIndex];
            await _viewModel.ChangeLanguageCommand.ExecuteAsync(langCode);
        }
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        if (_mapControl?.Map?.Navigator == null) return;
        _mapControl.Map.Navigator.ZoomIn(300);
        _mapControl.Map.Refresh();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        if (_mapControl?.Map?.Navigator == null) return;
        _mapControl.Map.Navigator.ZoomOut(300);
        _mapControl.Map.Refresh();
    }

    private void OnLocateMeClicked(object? sender, EventArgs e)
    {
        if (_mapControl?.Map?.Navigator == null) return;
        var loc = _viewModel.CurrentLocation;
        if (loc == null) return;
        var center = SphericalMercator.FromLonLat(loc.Longitude, loc.Latitude).ToMPoint();
        _mapControl.Map.Navigator.CenterOnAndZoomTo(center, ZoomResolution(17), 300);
        _mapControl.Map.Refresh();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Sync ngôn ngữ nếu bị lệch (ví dụ từ WelcomePage đổi ngôn ngữ rồi navigate vào)
            var currentLang = LocalizationResourceManager.Instance.CurrentLanguage;
            if (!string.IsNullOrEmpty(currentLang) && currentLang != _viewModel.SelectedLanguage)
            {
                await _viewModel.ChangeLanguageCommand.ExecuteAsync(currentLang);
            }

            // Restore map viewport nếu đã save trước đó
            if (_mapControl?.Map != null && !double.IsNaN(_savedCenterX))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _mapControl.Map.Navigator.CenterOnAndZoomTo(
                            new Mapsui.MPoint(_savedCenterX, _savedCenterY),
                            _savedResolution);
                        System.Diagnostics.Debug.WriteLine($"Viewport restored: center=({_savedCenterX:F0},{_savedCenterY:F0}) res={_savedResolution:F2}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Viewport restore error: {ex}");
                    }
                });
            }

            // Redraw markers (layer data is intact, just need refresh)
            UpdatePOIMarkers();
            UpdateCurrentLocationMarker();

            // Restart GPS tracking nếu đã dừng
            if (!_viewModel.IsTracking)
            {
                try { await _viewModel.StartTrackingCommand.ExecuteAsync(null); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnAppearing tracking restart error: {ex}"); }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAppearing error: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Save viewport position trước khi rời tab
        if (_mapControl?.Map != null)
        {
            try
            {
                var vp = _mapControl.Map.Navigator.Viewport;
                _savedCenterX = vp.CenterX;
                _savedCenterY = vp.CenterY;
                _savedResolution = vp.Resolution;
                System.Diagnostics.Debug.WriteLine($"Viewport saved: center=({_savedCenterX:F0},{_savedCenterY:F0}) res={_savedResolution:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Viewport save error: {ex}");
            }
        }

        _viewModel.StopTrackingCommand.Execute(null);
        _viewModel.GeofencePOITriggered -= OnGeofencePOITriggered;
    }

    private async void OnGeofencePOITriggered(object? sender, VK.Mobile.Models.POIModel poi)
    {
        try
        {
            // Đóng NowPlayingPage đang mở (nếu có)
            NowPlayingViewModel.RequestAutoClose();
            await Task.Delay(350); // chờ dismiss animation

            // Lấy content player theo thứ tự ưu tiên: API -> cache offline -> description fallback
            var apiService = _serviceProvider.GetRequiredService<IApiService>();
            var offlineService = _serviceProvider.GetRequiredService<IOfflineContentService>();
            string audioText;

            try
            {
                var audio = await apiService.GetAudioForPOIAsync(poi.Id, _viewModel.SelectedLanguage);
                if (audio != null && !string.IsNullOrWhiteSpace(audio.TextContent))
                {
                    audioText = audio.TextContent;
                    await offlineService.CacheNarrationScriptAsync(
                        poi.Id,
                        audio.LanguageCode,
                        audio.TextContent,
                        audio.AudioFileUrl,
                        audio.DurationInSeconds);
                }
                else
                {
                    audioText = await offlineService.GetCachedNarrationTextAsync(poi.Id, _viewModel.SelectedLanguage)
                                ?? (string.IsNullOrWhiteSpace(poi.Description)
                                    ? poi.Name
                                    : $"{poi.Name}. {poi.Description}");
                }
            }
            catch
            {
                audioText = await offlineService.GetCachedNarrationTextAsync(poi.Id, _viewModel.SelectedLanguage)
                            ?? (string.IsNullOrWhiteSpace(poi.Description)
                                ? poi.Name
                                : $"{poi.Name}. {poi.Description}");
            }

            static string FormatDist(double? km) => km switch
            {
                null or 0 => "",
                < 0.1 => $"{(km.Value * 1000):F0}m away",
                _ => $"{km.Value:F1} km away"
            };

            var page = _serviceProvider.GetRequiredService<NowPlayingPage>();
            var vm = (NowPlayingViewModel)page.BindingContext;
            vm.SetAllPois(_viewModel.NearbyPOIs.Count > 0 ? _viewModel.NearbyPOIs : _viewModel.Pois);
            vm.Initialize(
                poi.Id,
                poi.Name,
                poi.CategoryName ?? string.Empty,
                poi.ImageUrl ?? string.Empty,
                audioText,
                _viewModel.SelectedLanguage,
                poi.Address ?? string.Empty,
                FormatDist(poi.DistanceKm));

            await Shell.Current.Navigation.PushModalAsync(page, animated: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnGeofencePOITriggered error: {ex}");
        }
    }
}