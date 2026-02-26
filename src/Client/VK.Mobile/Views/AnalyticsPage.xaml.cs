using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using VK.Mobile.Models;
using VK.Mobile.Services;
using VK.Mobile.ViewModels;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiBrush = Mapsui.Styles.Brush;

namespace VK.Mobile.Views;

public partial class AnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _viewModel;
    private readonly StorageService _storageService;
    private MapControl? _heatmapControl;

    public AnalyticsPage(AnalyticsViewModel viewModel, StorageService storageService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _storageService = storageService;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        InitHeatmap();
    }

    // ── Heatmap ────────────────────────────────────────────────────
    private void InitHeatmap()
    {
        var points = _viewModel.HeatmapPoints;

        if (_heatmapControl == null)
        {
            _heatmapControl = new MapControl();
            _heatmapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            HeatmapContainer.Content = _heatmapControl;
        }

        // Xóa các layer cũ (giữ OSM layer)
        var toRemove = _heatmapControl.Map.Layers
            .Where(l => l.Name != "OpenStreetMap")
            .ToList();
        foreach (var l in toRemove)
            _heatmapControl.Map.Layers.Remove(l);

        if (points.Count == 0)
            return;

        // Tạo layer với các điểm heatmap
        var heatLayer = new WritableLayer { Name = "Heatmap", Style = null };

        foreach (var pt in points)
        {
            var mPoint = SphericalMercator.FromLonLat(pt.Longitude, pt.Latitude);
            var feature = new PointFeature(new MPoint(mPoint.x, mPoint.y));
            feature.Styles.Add(new SymbolStyle
            {
                Fill = new MapsuiBrush(new MapsuiColor(255, 87, 34, 80)),
                SymbolScale = 0.4,
                SymbolType = SymbolType.Ellipse
            });
            heatLayer.Add(feature);
        }

        _heatmapControl.Map.Layers.Add(heatLayer);

        // Zoom vào vùng chứa tất cả điểm
        var extent = heatLayer.Extent;
        if (extent != null)
        {
            _heatmapControl.Map.Navigator.ZoomToBox(extent.Grow(200));
        }

        _heatmapControl.Map.RefreshGraphics();
    }

    private void OnClearHeatmapClicked(object sender, EventArgs e)
    {
        _storageService.ClearLocationHistory();
        _viewModel.HeatmapPoints = new();
        _viewModel.HeatmapPointCount = 0;

        // Xóa heatmap layer
        if (_heatmapControl != null)
        {
            var toRemove = _heatmapControl.Map.Layers
                .Where(l => l.Name != "OpenStreetMap")
                .ToList();
            foreach (var l in toRemove)
                _heatmapControl.Map.Layers.Remove(l);
            _heatmapControl.Map.RefreshGraphics();
        }
    }
}
