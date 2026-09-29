using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>The sensor reflects, which the papers leave out.</summary>
public class SensorGhostTests
{
    [Fact]
    public void TheSensorAddsItsGhostsAndLeavesThePapersAlone()
    {
        var lens = Lenses.BiconvexPlate();
        var without = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflects = false });
        var with = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflectance = 0.05 });
        Assert.Equal(10, with.Ghosts.Count);
        foreach (var g in without.Ghosts)
        {
            var same = with.Find(g.Path.Surfaces.ToArray())!;
            Assert.Equal(g.Efl, same.Efl, 12);
            Assert.Equal(g.Irradiance, same.Irradiance, 15);
        }
    }

    /// <summary>
    /// G5,4: off the sensor, back to the plate's back face, and to the sensor again - a flat
    /// mirror 45.25 behind the focus, so the ghost is the image defocused by twice that, a disc
    /// of radius 2 x 45.25 x (EPD/2)/EFL.
    /// </summary>
    [Fact]
    public void TheSensorAndThePlateMakeADefocusedCopyOfTheImage()
    {
        var r = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog);
        var g = r.Find(5, 4)!;
        Assert.Equal(r.Nominal.Efl, g.Efl, 9);
        Assert.Equal(2 * 45.25 * 5.0 / r.Nominal.Efl, Math.Abs(g.MarginalAtImage), 3);
        double rr = Math.Pow(0.5168 / 2.5168, 2), tt = 1 - rr;
        Assert.Equal(Math.Pow(tt, 4) * 0.05 * rr, g.Transmittance, 6);
    }

    [Fact]
    public void ACoatedLensSendsBackLess()
    {
        var lens = Lenses.BiconvexPlate();
        var bare = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflects = false });
        var coated = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflects = false, CoatedReflectance = 0.005 });
        var g = coated.Find(4, 3)!;
        Assert.Equal(Math.Pow(0.995, 4) * 0.005 * 0.005, g.Transmittance, 12);
        Assert.True(g.Irradiance < bare.Find(4, 3)!.Irradiance / 50);
    }
}
