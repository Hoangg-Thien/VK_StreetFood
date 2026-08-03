using VK.Core.Entities;
using VK.Core.Exceptions;

namespace VK.Core.Tests;

public class PointOfInterestTests
{
    [Fact]
    public void SetTriggerProfile_WithValidValues_SetsPropertiesCorrectly()
    {
        var poi = new PointOfInterest();

        poi.SetTriggerProfile(80, 65.5);

        Assert.Equal(80, poi.TriggerPriority);
        Assert.Equal(65.5, poi.TriggerRadiusMeters);
    }

    [Fact]
    public void SetTriggerProfile_WithNullRadius_SetsPropertiesCorrectly()
    {
        var poi = new PointOfInterest();

        poi.SetTriggerProfile(70, null);

        Assert.Equal(70, poi.TriggerPriority);
        Assert.Null(poi.TriggerRadiusMeters);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void SetTriggerProfile_WithInvalidPriority_ThrowsBusinessRuleViolationException(int invalidPriority)
    {
        var poi = new PointOfInterest();

        var ex = Assert.Throws<BusinessRuleViolationException>(() => poi.SetTriggerProfile(invalidPriority, 50));
        Assert.Contains("TriggerPriority", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void SetTriggerProfile_WithInvalidRadius_ThrowsBusinessRuleViolationException(double invalidRadius)
    {
        var poi = new PointOfInterest();

        var ex = Assert.Throws<BusinessRuleViolationException>(() => poi.SetTriggerProfile(50, invalidRadius));
        Assert.Contains("TriggerRadiusMeters", ex.Message);
    }
}
