using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.RayTrace;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>
/// Rays aimed at the stop. A fast lens's pupil aberrations make its paraxial entrance pupil
/// overfill its real stop: US 8,264,785's Example 4, at F/1.6, stops an axial ray at 0.97 of the
/// paraxial pupil. Aimed, each ray crosses the stop where the pupil grid names, and the pupil's
/// area is the light the stop really lets in.
/// </summary>
public class AimingTests
{
    private static readonly Lazy<(AberrationCalculator.Core.Models.OpticalSystem Lens, double[] N, ParaxialResult P, int Stop)> Ex4 = new(() =>
    {
        var lens = Lenses.Read("TestData/US8264785_Ex4.zmx");
        var n = IndexResolver.Build(lens, Lenses.Catalog, lens.Wavelengths[lens.PrimaryWavelengthIndex].Value);
        return (lens, n, ParaxialTrace.Trace(lens, n, 0.0), lens.StopSurfaceIndex);
    });

    private static GhostTracer Rays(bool aim)
    {
        var (lens, n, p, stop) = Ex4.Value;
        int count = lens.Surfaces.Count;
        return new GhostTracer(lens, n, p, new double[count], stop, new bool[count], null, null, aim);
    }

    private static (double Y, double X) AtStop(GhostTracer rays, double field, double py, double px)
    {
        (double, double) at = (double.NaN, double.NaN);
        int stop = Ex4.Value.Stop;
        rays.Trace(field, py, px, clip: false, (j, h) => { if (j == stop) at = (h.Y, h.X); });
        return at;
    }

    /// <summary>Aimed, every ray around the pupil's rim, at every field, crosses the stop at its rim, where the grid names.</summary>
    [Fact]
    public void AnAimedRayCrossesTheStopWhereThePupilGridNamesIt()
    {
        var rays = Rays(aim: true);
        double radius = Ex4.Value.P.Y[Ex4.Value.Stop];
        foreach (double field in new[] { 0.0, 10.0, 20.0 })
            for (int k = 0; k < 12; k++)
            {
                double t = 2 * Math.PI * k / 12, py = Math.Cos(t), px = Math.Sin(t);
                var (y, x) = AtStop(rays, field, py, px);
                Assert.True(Math.Abs(py * radius - y) < 1e-6 && Math.Abs(px * radius - x) < 1e-6,
                            $"{field} deg, ({px:0.00}, {py:0.00}): at ({x}, {y}) on the stop");
            }
        // And the chief ray through the stop's centre.
        var chief = AtStop(rays, 20.0, 0.0, 0.0);
        Assert.True(Math.Abs(chief.Y) < 1e-6, $"{chief.Y}");
    }

    /// <summary>Unaimed, the paraxial pupil's rim ray overshoots the stop: what the aiming is for.</summary>
    [Fact]
    public void TheParaxialPupilOverfillsAFastLensesStop()
    {
        double radius = Math.Abs(Ex4.Value.P.Y[Ex4.Value.Stop]);
        var (y, _) = AtStop(Rays(aim: false), 0.0, 1.0, 0.0);
        Assert.True(Math.Abs(y) > 1.02 * radius, $"{y} against the stop's {radius}");
    }

    /// <summary>
    /// On axis the real pupil is a disc, whose radius is where an unaimed ray reaches the stop's
    /// rim - found by bisection, with no aiming - and its area that radius squared.
    /// </summary>
    [Fact]
    public void OnAxisThePupilsAreaIsTheRadiusThatReachesTheRimSquared()
    {
        var plain = Rays(aim: false);
        double radius = Math.Abs(Ex4.Value.P.Y[Ex4.Value.Stop]);
        double lo = 0.5, hi = 1.2;
        for (int k = 0; k < 60; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (Math.Abs(AtStop(plain, 0.0, mid, 0.0).Y) < radius) lo = mid; else hi = mid;
        }
        double area = Rays(aim: true).PupilArea(0.0);
        Assert.Equal(lo * lo, area, 4);
        Assert.InRange(area, 0.85, 0.97);
        Assert.Equal(1.0, plain.PupilArea(0.0));
    }

    /// <summary>The analysis aims by default, and not when asked not to.</summary>
    [Fact]
    public void AimingCanBeTurnedOff()
    {
        var lens = Lenses.Read("TestData/US8264785_Ex4.zmx");
        var fields = new[] { 0.0 };
        var aimed = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = fields, RealRays = false });
        var plain = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = fields, RealRays = false, AimRays = false });
        Assert.True(aimed.Ghosts.All(g => g.Tracer.Aims));
        Assert.True(plain.Ghosts.All(g => !g.Tracer.Aims));
    }
}
