using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITourSessionService
{
    TourModel? ActiveTour { get; }
    IReadOnlyCollection<int> ActivePoiIds { get; }
    event EventHandler? ActiveTourChanged;
    void SetActiveTour(TourModel tour);
    void ClearActiveTour();
}

public class TourSessionService : ITourSessionService
{
    private readonly HashSet<int> _activePoiIds = new();

    public TourModel? ActiveTour { get; private set; }

    public IReadOnlyCollection<int> ActivePoiIds => _activePoiIds;

    public event EventHandler? ActiveTourChanged;

    public void SetActiveTour(TourModel tour)
    {
        ActiveTour = tour;
        _activePoiIds.Clear();

        foreach (var poiId in tour.Points.Select(p => p.PoiId).Where(id => id > 0))
            _activePoiIds.Add(poiId);

        ActiveTourChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearActiveTour()
    {
        ActiveTour = null;
        _activePoiIds.Clear();
        ActiveTourChanged?.Invoke(this, EventArgs.Empty);
    }
}
