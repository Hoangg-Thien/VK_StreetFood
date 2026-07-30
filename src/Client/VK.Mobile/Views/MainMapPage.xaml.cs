using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using BruTile.Cache;
using BruTile.Predefined;
using System.Globalization;
using VK.Mobile.Helpers;
using VK.Mobile.ViewModels;
using VK.Mobile.Models;
using VK.Mobile.Services;

namespace VK.Mobile.Views;

public partial class MainMapPage : ContentPage
{
    private readonly MainMapViewModel _viewModel;
    private readonly IRoutingService _routingService;
    private MapControl? _mapControl;
    private WritableLayer? _poiLayer;
    private WritableLayer? _routeLayer;
    private WritableLayer? _locationLayer;
    private bool _isRouting = false;
    private bool _hasActiveRoute = false;
    private bool _hasCenteredOnUser = false;
    private POIModel? _selectedPoi;
    private static LocalizationResourceManager L => LocalizationResourceManager.Instance;
    // Viewport save/restore on tab switch
    private double _savedCenterX = double.NaN;
    private double _savedCenterY = double.NaN;
    private double _savedResolution = double.NaN;

    // OSM resolution for zoom level: 156543.03392804062 / 2^z
    private static double ZoomResolution(int level) =>
        156543.03392804062 / Math.Pow(2, level);

    private static bool IsOfflineMode => Connectivity.NetworkAccess != NetworkAccess.Internet;

    public MainMapPage(
        MainMapViewModel viewModel,
        IRoutingService routingService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _routingService = routingService;
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

            // Direction route polyline layer
            _routeLayer = new WritableLayer { Name = "Route", Style = null };
            map.Layers.Add(_routeLayer);

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
                Text = string.Format(CultureInfo.CurrentCulture, L["MainMapMapLoadFailedFormat"], ex.Message),
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

    private void DrawRoutePolyline(RouteResultModel route)
    {
        if (_routeLayer == null || _mapControl?.Map == null || route.Coordinates.Count < 2)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _routeLayer.Clear();

                var projectedCoordinates = route.Coordinates
                    .Select(point => SphericalMercator.FromLonLat(point.Longitude, point.Latitude))
                    .Select(point => point.ToMPoint())
                    .Select(point => new Coordinate(point.X, point.Y))
                    .ToArray();

                if (projectedCoordinates.Length < 2)
                    return;

                var feature = new GeometryFeature
                {
                    Geometry = new LineString(projectedCoordinates)
                };

                feature.Styles.Add(new VectorStyle
                {
                    Line = new Pen(Mapsui.Styles.Color.FromString("#1565C0"), 5),
                    Outline = new Pen(Mapsui.Styles.Color.White, 2)
                });

                _routeLayer.Add(feature);

                if (feature.Extent != null)
                {
                    _mapControl.Map.Navigator.ZoomToBox(feature.Extent, MBoxFit.Fit, 80);
                }

                _hasActiveRoute = true;
                ClearRouteBorder.IsVisible = true;
                _mapControl.Map.Refresh();
                System.Diagnostics.Debug.WriteLine($"Route drawn: {route.DistanceMeters:F0}m, {route.DurationSeconds:F0}s, points={route.Coordinates.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DrawRoutePolyline error: {ex}");
            }
        });
    }

    private void ClearCurrentRoute()
    {
        if (_routeLayer == null || _mapControl?.Map == null || !_hasActiveRoute)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _routeLayer.Clear();
                _mapControl.Map.Refresh();

                _hasActiveRoute = false;
                ClearRouteBorder.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearCurrentRoute error: {ex}");
            }
        });
    }

    private void MapControl_Info(object? sender, MapInfoEventArgs e)
    {
        if (_poiLayer == null || _mapControl == null) return;

        try
        {
            var screenPos = e.ScreenPosition;
            var worldPos = _mapControl.Map.Navigator.Viewport.ScreenToWorld(screenPos.X, screenPos.Y);
            var worldTapTolerance = _mapControl.Map.Navigator.Viewport.Resolution * 12; // ~12px tap radius

            PointFeature? closest = null;
            double minDist = double.MaxValue;

            foreach (var f in _poiLayer.GetFeatures())
            {
                if (f is PointFeature pf)
                {
                    var dx = pf.Point.X - worldPos.X;
                    var dy = pf.Point.Y - worldPos.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < worldTapTolerance && dist < minDist)
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
                    MainThread.BeginInvokeOnMainThread(() => ShowPOIBottomCard(poi));
                return;
            }

            MainThread.BeginInvokeOnMainThread(HidePOIBottomCard);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapControl_Info error: {ex}");
        }
    }

    private void ShowPOIBottomCard(POIModel poi)
    {
        _selectedPoi = poi;

        POICardName.Text = poi.Name ?? string.Empty;
        POICardCategory.Text = poi.CategoryName ?? string.Empty;

        // Tính khoảng cách: dùng DistanceKm nếu đã có, không thì tính từ vị trí hiện tại
        double? distKm = poi.DistanceKm > 0 ? poi.DistanceKm : null;
        if (distKm == null && _viewModel.CurrentLocation is { } loc
            && (poi.Latitude != 0 || poi.Longitude != 0))
        {
            distKm = GeoHelper.HaversineKm(loc.Latitude, loc.Longitude, poi.Latitude, poi.Longitude);
        }
        POICardDistance.Text = DistanceFormatter.Format(distKm);

        // Ảnh POI
        POICardImage.Source = !string.IsNullOrWhiteSpace(poi.ImageUrl)
            ? ImageSource.FromUri(new Uri(poi.ImageUrl))
            : "icon_food.png";

        POIBottomCard.IsVisible = true;
    }

    private void HidePOIBottomCard()
    {
        POIBottomCard.IsVisible = false;
        _selectedPoi = null;
    }

    private void OnCardBackdropTapped(object? sender, TappedEventArgs e) => HidePOIBottomCard();
    private void OnCardCloseTapped(object? sender, EventArgs e) => HidePOIBottomCard();

    private async void OnPOIListenClicked(object? sender, EventArgs e)
    {
        if (_selectedPoi == null) return;
        var poi = _selectedPoi;
        HidePOIBottomCard();
        await _viewModel.TestAudioCommand.ExecuteAsync(poi);
    }

    private async void OnPOIDetailClicked(object? sender, EventArgs e)
    {
        if (_selectedPoi == null) return;
        var poi = _selectedPoi;
        HidePOIBottomCard();
        await _viewModel.POISelectedCommand.ExecuteAsync(poi);
    }

    private async void OnPOIDirectionsClicked(object? sender, EventArgs e)
    {
        if (_isRouting) return;
        if (_selectedPoi == null || (_selectedPoi.Latitude == 0 && _selectedPoi.Longitude == 0)) return;

        var poi = _selectedPoi;
        var currentLocation = _viewModel.CurrentLocation;
        HidePOIBottomCard();

        if (currentLocation == null)
        {
            await DisplayAlertAsync(L["Error"], L["MainMapCurrentLocationUnavailable"], L["MainMapClose"]);
            return;
        }

        _isRouting = true;
        try
        {
            var route = await _routingService.GetDrivingRouteAsync(
                currentLocation.Latitude,
                currentLocation.Longitude,
                poi.Latitude,
                poi.Longitude);

            if (route == null || route.Coordinates.Count < 2)
            {
                await DisplayAlertAsync(L["Error"], L["MainMapRouteUnavailable"], L["MainMapClose"]);
                return;
            }

            DrawRoutePolyline(route);

            if (route.Provider == "offline-graph")
            {
                await DisplayAlertAsync(L["SettingsOfflineTitle"], L["MainMapOfflineRouteGraphMessage"], L["MainMapClose"]);
            }
            else if (route.Provider == "offline-fallback")
            {
                await DisplayAlertAsync(L["SettingsOfflineTitle"], L["MainMapOfflineRouteApproxMessage"], L["MainMapClose"]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Directions error: {ex}");
            await DisplayAlertAsync(L["Error"], L["MainMapDirectionsError"], L["MainMapClose"]);
        }
        finally
        {
            _isRouting = false;
        }
    }

    private void OnClearRouteClicked(object? sender, EventArgs e)
    {
        ClearCurrentRoute();
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
            // Sync ngôn ngữ nếu bị lệch (từ WelcomePage) — chỉ update property,
            // không gọi API hay reload POI để tránh lag
            var currentLang = LocalizationResourceManager.Instance.CurrentLanguage;
            if (!string.IsNullOrEmpty(currentLang) && currentLang != _viewModel.SelectedLanguage)
            {
                _viewModel.SelectedLanguage = currentLang;
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

    }
}