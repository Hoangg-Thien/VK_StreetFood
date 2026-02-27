using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using VK.Mobile.ViewModels;
using VK.Mobile.Models;

namespace VK.Mobile.Views;

public partial class MainMapPage : ContentPage
{
    private readonly MainMapViewModel _viewModel;
    private MapControl? _mapControl;
    private WritableLayer? _poiLayer;
    private WritableLayer? _locationLayer;
    private bool _hasCenteredOnUser = false;

    // OSM resolution for zoom level: 156543.03392804062 / 2^z
    private static double ZoomResolution(int level) =>
        156543.03392804062 / Math.Pow(2, level);

    public MainMapPage(MainMapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            InitializeMap();

            try { await _viewModel.InitializeCommand.ExecuteAsync(null); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"VM init error: {ex}"); }

            // Wire up collection / property changes
            _viewModel.Pois.CollectionChanged += (_, _) => UpdatePOIMarkers();
            _viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.CurrentLocation))
                    UpdateCurrentLocationMarker();
            };

            // Draw any POIs that were loaded during InitializeAsync
            if (_viewModel.Pois.Count > 0)
                UpdatePOIMarkers();

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

            // Tile layer (OpenStreetMap)
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

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

                foreach (var poi in _viewModel.Pois)
                {
                    var point = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                    var feature = new PointFeature(point.ToMPoint());

                    feature["poi_id"] = poi.Id;
                    feature["poi_name"] = poi.Name;

                    // Orange marker
                    feature.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 0.8,
                        Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#FF5722")),
                        Outline = new Pen(Mapsui.Styles.Color.White, 2),
                        SymbolType = SymbolType.Ellipse
                    });

                    // Label below marker
                    feature.Styles.Add(new LabelStyle
                    {
                        Text = poi.Name,
                        ForeColor = Mapsui.Styles.Color.FromString("#333333"),
                        BackColor = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(255, 255, 255, 200)),
                        Font = new Mapsui.Styles.Font { Size = 10 },
                        HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                        Offset = new Offset(0, -20)
                    });

                    _poiLayer.Add(feature);
                }

                _mapControl.Map.Refresh();
                System.Diagnostics.Debug.WriteLine($"Updated {_viewModel.Pois.Count} POI markers");
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
                _locationLayer.Clear();

                var point = SphericalMercator.FromLonLat(
                    _viewModel.CurrentLocation.Longitude,
                    _viewModel.CurrentLocation.Latitude);
                var mpoint = point.ToMPoint();

                // Outer ring
                var outerFeature = new PointFeature(mpoint);
                outerFeature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.2,
                    Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(33, 150, 243, 40)),
                    Outline = new Pen(Mapsui.Styles.Color.FromString("#2196F3"), 1),
                    SymbolType = SymbolType.Ellipse
                });
                _locationLayer.Add(outerFeature);

                // Blue dot
                var dotFeature = new PointFeature(mpoint);
                dotFeature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.4,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#2196F3")),
                    Outline = new Pen(Mapsui.Styles.Color.White, 3),
                    SymbolType = SymbolType.Ellipse
                });
                _locationLayer.Add(dotFeature);

                _mapControl.Map.Refresh();

                // Center on user first time
                if (!_hasCenteredOnUser)
                {
                    _hasCenteredOnUser = true;
                    var res = ZoomResolution(16);
                    _mapControl.Map.Navigator.CenterOnAndZoomTo(mpoint, res);
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
                    var action = await DisplayActionSheet(
                        poi.Name,
                        "Đóng",
                        null,
                        " Nghe thuyết minh",
                        " Xem chi tiết");

                    if (action == " Nghe thuyết minh")
                    {
                        await _viewModel.TestAudioCommand.ExecuteAsync(poi);
                    }
                    else if (action == " Xem chi tiết")
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

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        await _viewModel.OpenQRScannerCommand.ExecuteAsync(null);
    }

    private async void OnLanguageChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var langCode = AppSettings.SupportedLanguages[picker.SelectedIndex];
            await _viewModel.ChangeLanguageCommand.ExecuteAsync(langCode);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopTrackingCommand.Execute(null);
    }
}