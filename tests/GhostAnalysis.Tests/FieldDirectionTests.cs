using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>Fields swept along the sensor's height, its width, its diagonal, or at any angle.</summary>
public class FieldDirectionTests
{
    private const double Lambda = 0.5875618;

    [Theory]
    [InlineData(0.0, 12.0)]                  // along the height: half of 24
    [InlineData(90.0, 18.0)]                 // along the width: half of 36
    [InlineData(45.0, 16.970562748477143)]   // 12 / cos 45: the long side's edge first
    public void TheSensorReachesItsEdgeAlongTheField(double angle, double reach)
    {
        Assert.Equal(reach, new Sensor { Width = 36, Height = 24, FieldAngle = angle }.HalfExtentAlongField, 9);
    }

    /// <summary>Along the diagonal, the sensor reaches its corner: half of √(36² + 24²).</summary>
    [Fact]
    public void TheDiagonalReachesTheCorner()
    {
        var sensor = new Sensor { Width = 36, Height = 24, FieldAngle = null };
        Assert.Equal(0.5 * Math.Sqrt(36 * 36 + 24 * 24), sensor.HalfExtentAlongField, 9);
        Assert.True(sensor.Covers(0.0, 21.6));
        Assert.False(sensor.Covers(0.0, 21.7));
        Assert.Equal(Math.Sqrt(0.5) * 20, new Sensor { FieldAngle = null, PeriodX = 5 }.ToSensor(0, 20).Along, 9);  // no size: 45 degrees
    }

    [Fact]
    public void AlongTheWidthTheFieldReachesFurther()
    {
        var height = new Sensor { Width = 36, Height = 24, FieldAngle = 0.0 };
        var width = new Sensor { Width = 36, Height = 24, FieldAngle = 90.0 };
        Assert.False(height.Covers(0.0, 15.0));
        Assert.True(width.Covers(0.0, 15.0));
        // Across the field, the other way round.
        Assert.True(height.Covers(15.0, 0.0));
        Assert.False(width.Covers(15.0, 0.0));
    }

    /// <summary>
    /// The grating's orders stay tied to the sensor: with the field along the width, order (+1,0)
    /// - along the width - turns the light in the field's plane, and (0,+1) across it.
    /// </summary>
    [Fact]
    public void TheOrdersTurnWithTheSensor()
    {
        var r = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions
        {
            Fields = new[] { 0.0 },
            Sensor = new Sensor { PeriodX = 8, MaxOrder = 1, FieldAngle = 90.0 },
        });
        double shift = 2 * 45.25 * Lambda / 8.0;
        var alongWidth = r.Find(new[] { 5, 4 }, (1, 0))!.Fields[0];
        Assert.Equal(shift, Math.Abs(alongWidth.ParaxialCenter), 9);
        Assert.Equal(0.0, alongWidth.ParaxialCenterX, 9);
        var alongHeight = r.Find(new[] { 5, 4 }, (0, 1))!.Fields[0];
        Assert.Equal(0.0, alongHeight.ParaxialCenter, 9);
        Assert.Equal(shift, Math.Abs(alongHeight.ParaxialCenterX), 9);
        // The kicks are the same size whichever way the sensor lies.
        var k = new Sensor { PeriodX = 8, FieldAngle = 30.0 }.Kick(1, 1, Lambda, 1.0);
        Assert.Equal(Math.Sqrt(2) * Lambda / 8.0, Math.Sqrt(k.L * k.L + k.M * k.M), 12);
    }

    /// <summary>
    /// The Cooke at 14 degrees on a 36 x 24 sensor: along its height the field is past the edge
    /// (the image at 13.1, the edge at 12), along its width inside it (the edge at 18) - and G6,1,
    /// landing on the other side, loses less of its light off the edge.
    /// </summary>
    [Fact]
    public void AlongTheWidthTheCookesGhostStaysOnTheSensor()
    {
        GhostField At(double angle) =>
            GhostAnalyzer.Analyze(Lenses.Read("TestData/Cooke_40deg_FC.zmx"), Lenses.Catalog, new GhostOptions
            {
                CoatedReflectance = 0.005, Fields = new[] { 14.0 },
                Sensor = new Sensor { Width = 36, Height = 24, FieldAngle = angle },
            }).Find(6, 1)!.Fields.Single(f => f.Field == 14.0);
        var height = At(0.0);
        var width = At(90.0);
        Assert.True(width.Transmitted > height.Transmitted, $"{width.Transmitted} along the width, {height.Transmitted} along the height");
    }
}
