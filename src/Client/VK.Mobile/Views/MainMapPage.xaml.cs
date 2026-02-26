using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using VK.Mobile.ViewModels;
using VK.Mobile.Models;

namespace VK.Mobile.Views;

public partial class MainMapPage : ContentPage
{
    private readonly MainMapViewModel _viewModel;
    private MapControl? _mapControl;
    private WritableLayer? _poiLayer;
    private WritableLayer? _locationLayer;

    public MainMapPage(MainMapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        InitializeMap();
        await _viewModel.InitializeCommand.ExecuteAsync(null);

        // Subscribe to POI changes
        _viewModel.Pois.CollectionChanged += (s, e) => UpdatePOIMarkers();

        // Subscribe to location changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.CurrentLocation))
            {
                UpdateCurrentLocationMarker();
            }
        };
    }

    private void InitializeMap()
    {
        _mapControl = new MapControl();

        // Create map
        var map = new Mapsui.Map();

        // Add OpenStreetMap layer
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        // Add POI layer
        _poiLayer = new WritableLayer
        {
            Name = "POIs",
            Style = null
        };
        map.Layers.Add(_poiLayer);

        // Add location layer
        _locationLayer = new WritableLayer
        {
            Name = "Location",
            Style = null
        };
        map.Layers.Add(_locationLayer);

        // Set initial position (Vinh Khanh Food Street)
        var center = SphericalMercator.FromLonLat(
            AppSettings.DefaultLongitude,
            AppSettings.DefaultLatitude);
        // map.Home = n => n.CenterOnAndZoomTo(center, AppSettings.DefaultZoomLevel);

        _mapControl.Map = map;
        _mapControl.Map.Navigator.CenterOn(center.ToMPoint());
        _mapControl.Map.Navigator.ZoomTo(AppSettings.DefaultZoomLevel);

        // Handle marker clicks
        _mapControl.Info += MapControl_Info;

        MapContainer.Content = _mapControl;
    }

    private void UpdatePOIMarkers()
    {
        if (_poiLayer == null || _mapControl?.Map == null)
            return;

        _poiLayer.Clear();

        foreach (var poi in _viewModel.Pois)
        {
            var point = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
            var feature = new PointFeature(point.ToMPoint());

            feature["poi_id"] = poi.Id;
            feature["poi_name"] = poi.Name;

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.7,
                Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#FF5722")),
                Outline = new Pen(Mapsui.Styles.Color.FromString("#FFFFFF"), 2)
            });

            _poiLayer.Add(feature);
        }

        _mapControl.Map.Refresh();
    }

    private void UpdateCurrentLocationMarker()
    {
        if (_locationLayer == null || _mapControl?.Map == null || _viewModel.CurrentLocation == null)
            return;

        _locationLayer.Clear();

        var point = SphericalMercator.FromLonLat(
            _viewModel.CurrentLocation.Longitude,
            _viewModel.CurrentLocation.Latitude);

        var feature = new PointFeature(point.ToMPoint());

        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.5,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#2196F3")),
            Outline = new Pen(Mapsui.Styles.Color.FromString("#FFFFFF"), 2)
        });

        _locationLayer.Add(feature);
        _mapControl.Map.Refresh();

        // Center map on location (first time only)
        if (_viewModel.Pois.Count == 0)
        {
            _mapControl.Map.Navigator.CenterOn(point.ToMPoint());
        }
    }

    private async void MapControl_Info(object? sender, MapInfoEventArgs e)
    {
        // Try to get feature from the POI layer
        if (_poiLayer == null)
            return;

        var features = _poiLayer.GetFeatures();
        foreach (var feature in features)
        {
            if (feature is PointFeature pointFeature)
            {
                var poiId = pointFeature["poi_id"];
                if (poiId is int id)
                {
                    var poi = _viewModel.Pois.FirstOrDefault(p => p.Id == id);
                    if (poi != null)
                    {
                        // Check if clicked near this feature
                        // For simplicity, just open the first feature for now
                        await _viewModel.POISelectedCommand.ExecuteAsync(poi);
                        return;
                    }
                }
            }
        }
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        await _viewModel.OpenQRScannerCommand.ExecuteAsync(null);
    }

    private async void OnLanguageChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is string language)
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
