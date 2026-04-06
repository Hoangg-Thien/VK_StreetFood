namespace VK.Mobile.Models;

public class RoutePointModel
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class RouteResultModel
{
    public List<RoutePointModel> Coordinates { get; set; } = new();
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public string Provider { get; set; } = "osrm";
}