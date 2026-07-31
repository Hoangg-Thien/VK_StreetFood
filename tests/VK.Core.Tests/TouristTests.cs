using VK.Core.Entities;

namespace VK.Core.Tests;

public class TouristTests
{
    [Fact]
    public void Tourist_ShouldHaveDefaultValues_WhenCreated()
    {
        // Arrange & Act
        var tourist = new Tourist();

        // Assert
        Assert.Equal("vi", tourist.PreferredLanguage);
        Assert.Equal(0, tourist.TotalVisits);
        Assert.Empty(tourist.VisitLogs);
        Assert.Empty(tourist.Favorites);
        Assert.Empty(tourist.Ratings);
        Assert.Empty(tourist.Analytics);
    }
}
