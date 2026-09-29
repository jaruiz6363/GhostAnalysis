using AberrationCalculator.Core.Glass;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>
/// Ghosts that are out of focus on axis and in focus off it: their image surfaces cross the
/// sensor (2011, section 12). Third-order theory predicts where; real rays find where.
/// </summary>
public class FocusCrossingTests
{
    private static readonly double[] HalfDegrees = Enumerable.Range(0, 21).Select(i => i * 0.5).ToArray();

    /// <summary>
    /// The paper's lens with the sensor at 43.0 behind the plate, not 45.25: just short of where
    /// G4,3, the plate's ghost, focuses (43.94). On axis it is 0.94 out of focus; its image
    /// surfaces curve back towards the lens off axis, and cross the sensor.
    /// </summary>
    private static readonly Lazy<GhostResult> ShortSensor = new(() =>
    {
        var lens = Lenses.BiconvexPlate();
        lens.Surfaces[4].Thickness = 43.0;
        return GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflects = false, Fields = HalfDegrees });
    });

    [Fact]
    public void ThirdOrderAndRealRaysAgreeOnWhereTheGhostFocuses()
    {
        var g = ShortSensor.Value.Find(4, 3)!;
        Assert.Equal(-0.9402, g.DeltaZ, 4);
        Assert.Equal(new[] { "T", "S" }, g.Crossings.Select(x => x.Kind));
        double realT = g.Crossings[0].Field, realS = g.Crossings[1].Field;
        // Tangential first: its surface curves three times as fast.
        Assert.InRange(realT, 4.0, 4.7);
        Assert.InRange(realS, 6.0, 6.7);
        Assert.InRange(g.PredictedTangentialCrossing / realT, 0.99, 1.01);
        Assert.InRange(g.PredictedSagittalCrossing / realS, 0.99, 1.01);
    }

    /// <summary>
    /// Near the axis the real foci are the third-order ones: at 1 degree their departure from the
    /// axial focus agrees to 1 per cent. On axis both are ΔZ.
    /// </summary>
    [Fact]
    public void TheRealFociAreTheThirdOrderOnesNearTheAxis()
    {
        var g = ShortSensor.Value.Find(4, 3)!;
        var axis = g.Fields.Single(f => f.Field == 0);
        Assert.Equal(g.DeltaZ, axis.TangentialFocus, 5);
        Assert.Equal(g.DeltaZ, axis.SagittalFocus, 5);

        var one = g.Fields.Single(f => f.Field == 1.0);
        Assert.InRange((one.TangentialFocus - g.DeltaZ) / (one.PredictedTangentialFocus - g.DeltaZ), 0.99, 1.01);
        Assert.InRange((one.SagittalFocus - g.DeltaZ) / (one.PredictedSagittalFocus - g.DeltaZ), 0.99, 1.01);
    }

    /// <summary>
    /// The point of it all: the ghost is several times brighter off axis than on it, brightest
    /// between the axis and its tangential crossing, where its spot is smallest - and the fine
    /// scan finds that field though no field of the sweep falls on it.
    /// </summary>
    [Fact]
    public void TheGhostIsBrightestOffAxis()
    {
        var g = ShortSensor.Value.Find(4, 3)!;
        var axis = g.Fields.Single(f => f.Field == 0);
        var peak = g.Peak!;
        Assert.InRange(peak.Field, 3.0, g.Crossings[0].Field);
        Assert.True(peak.Irradiance > 3 * axis.Irradiance, $"{peak.Irradiance} against {axis.Irradiance} on axis");
        Assert.True(peak.RmsRadius < 0.6 * axis.RmsRadius);
        Assert.Contains(g.Fields, f => f.Marker == "P");
        Assert.Equal("G4,3", ShortSensor.Value.Ranked.First().Name);
    }

    /// <summary>With the sensor where the paper has it, beyond the plate ghost's focus, nothing crosses.</summary>
    [Fact]
    public void NoGhostOfThePapersLensFocusesOffAxis()
    {
        var r = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog,
                                      new GhostOptions { ImageReflects = false, Fields = HalfDegrees });
        foreach (var g in r.Ghosts)
        {
            Assert.Empty(g.Crossings);
            Assert.True(double.IsNaN(g.PredictedTangentialCrossing), g.Name);
        }
    }

    /// <summary>
    /// A focus changes sign by passing through infinity too, where the ghost's rays leave the lens
    /// parallel; that is not a crossing. G7,2 of the Cooke triplet does both within a tenth of a
    /// degree: its tangential focus is on the sensor at 7.30 and at infinity at 7.42.
    /// </summary>
    [Fact]
    public void AFocusThroughInfinityIsNoCrossing()
    {
        var lens = Lenses.Read("TestData/Cooke_40deg_FC.zmx");
        var r = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { CoatedReflectance = 0.005, Fields = new[] { 0.0, 10.0 } });
        var g = r.Find(7, 2)!;
        Assert.Contains(g.Crossings, x => x.Kind == "T" && Math.Abs(x.Field - 7.30) < 0.01);
        Assert.DoesNotContain(g.Crossings, x => x.Field > 7.35 && x.Field < 7.5);
        // Every crossing found, in every ghost, is a focus on the sensor.
        foreach (var ghost in r.Ghosts)
        {
            foreach (var (field, kind) in ghost.Crossings)
            {
                var f = ghost.Tracer.Foci(field);
                Assert.True(Math.Abs(kind == "T" ? f.Tangential : f.Sagittal) < 1e-4, $"{ghost.Name} {kind} {field}");
            }
        }
    }
}
