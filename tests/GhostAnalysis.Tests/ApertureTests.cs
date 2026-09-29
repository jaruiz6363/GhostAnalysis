using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>
/// Where the glass ends and where the sensor does, when the lens file does not say. A surface
/// with no semi-diameter used to be endless, and a sensor with no size too: a ghost could reflect
/// from a surface's sphere far beyond the lens, and land far beyond the sensor, and still count.
/// </summary>
public class ApertureTests
{
    /// <summary>The paper's lens, stripped of its semi-diameters, with a field of 5 degrees.</summary>
    private static OpticalSystem Bare()
    {
        var lens = Lenses.BiconvexPlate();
        foreach (var s in lens.Surfaces) s.SemiDiameter = 0.0;
        lens.Fields.Clear();
        lens.Fields.Add(new Field(0.0));
        lens.Fields.Add(new Field(5.0));
        return lens;
    }

    /// <summary>A surface the file does not size is sized to the lens's own beam: its full pupil over its fields.</summary>
    [Fact]
    public void AnUnsizedSurfaceIsSizedToTheBeam()
    {
        var lens = Bare();
        var r = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 } });
        var n = r.NominalIndices.ToArray();
        int count = lens.Surfaces.Count;
        var rays = new GhostTracer(lens, n, r.Nominal, new double[count], 1, new bool[count], null, null, aim: true);
        for (int i = 2; i <= 4; i++)               // surface 1 is the stop, below
        {
            // Every ray of the lens's own beam, aimed at its stop, passes; the largest just touches.
            double reach = 0;
            foreach (double h in new[] { 0.0, 5.0 })
                foreach (double py in new[] { -1.0, 1.0 })
                    rays.Trace(h, py, 0.0, clip: false, (j, hit) => { if (j == i) reach = Math.Max(reach, Math.Abs(hit.Y)); });
            Assert.True(r.Apertures[i] >= reach - 1e-12, $"surface {i}: {r.Apertures[i]} against a ray at {reach}");
            Assert.True(r.Apertures[i] < reach * 1.02, $"surface {i}: {r.Apertures[i]}, the beam reaching {reach}");
        }
        // The stop is the stop, sized by the marginal ray, not by the whole beam.
        Assert.False(r.ApertureComputed[1]);
        Assert.Equal(5.0, r.Apertures[1], 9);
        Assert.True(r.ApertureComputed[2] && r.ApertureComputed[3] && r.ApertureComputed[4]);
    }

    /// <summary>
    /// The apertures pass the lens's full beam at every field up to its largest - every ray of a
    /// full-pupil grid, none clipped - and clip it beyond. Checked on the Cooke triplet stripped of
    /// its semi-diameters, at 20 degrees.
    /// </summary>
    [Fact]
    public void TheLensPassesItsWholeFieldAndNoMore()
    {
        var lens = Lenses.Read("TestData/Cooke_40deg_FC.zmx");
        foreach (var s in lens.Surfaces) s.SemiDiameter = 0.0;
        var r = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 }, RealRays = false });
        var n = r.NominalIndices.ToArray();
        int last = lens.Surfaces.Count - 2, stop = lens.StopSurfaceIndex;

        int Clipped(double field)
        {
            int clipped = 0;
            for (int i = 0; i < 41; i++)
                for (int j = 0; j < 41; j++)
                {
                    double py = -1 + i / 20.0, px = -1 + j / 20.0;
                    if (px * px + py * py > 1) continue;
                    var hits = RealRayTrace.TraceRecord(lens, n, r.Nominal, field, py, px, atParaxialFocus: false);
                    for (int k = 1; k <= last; k++)
                        if (k != stop && Math.Sqrt(hits[k].X * hits[k].X + hits[k].Y * hits[k].Y) > r.Apertures[k] * (1 + 1e-9)) { clipped++; break; }
                }
            return clipped;
        }

        double top = lens.Fields.Max(f => f.Y);
        Assert.Equal(20.0, top);
        foreach (double field in new[] { 0.0, 7.0, 14.0, 17.3, 20.0 })
            Assert.Equal(0, Clipped(field));
        Assert.True(Clipped(24.0) > 0);
    }

    /// <summary>A size the file gives is kept.</summary>
    [Fact]
    public void AGivenSizeIsKept()
    {
        var r = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 } });
        Assert.Equal(new[] { 0.0, 5.0, 5.0, 20.0, 20.0 }, r.Apertures.Take(5));
        Assert.DoesNotContain(true, r.ApertureComputed);
    }

    /// <summary>
    /// The plate, sized to the lens's narrow converging beam, stops most of what G2,1 sends
    /// through it: that ghost leaves the lens spreading, to a disc 55 across at the image.
    /// </summary>
    [Fact]
    public void AGhostIsStoppedWhereTheGlassEnds()
    {
        // A sensor too large to matter, so only the glass stops anything; and the lens used on
        // axis only, so the plate is sized to the axial beam converging through it, about 2.3.
        var wide = new Sensor { Width = 1000, Height = 1000 };
        var axial = Bare();
        axial.Fields.RemoveAt(1);
        var sized = GhostAnalyzer.Analyze(axial, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 }, Sensor = wide });
        Assert.InRange(sized.Apertures[3], 2.3, 2.4);
        var endless = Bare();
        foreach (var s in endless.Surfaces.Skip(2).Take(3)) s.SemiDiameter = 1000;   // as if unsized
        var open = GhostAnalyzer.Analyze(endless, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 }, Sensor = wide });
        // The plate becomes the ghost's stop - found the 2009 paper's way - and its pupil, and so
        // its power, shrink to what gets through the plate.
        var cut = sized.Find(2, 1)!;
        var free = open.Find(2, 1)!;
        Assert.Equal(1, free.StopSurface);
        Assert.True(cut.StopSurface is 3 or 4, $"stopped at {cut.StopSurface}");
        Assert.True(cut.Power < 0.2 * free.Power, $"{cut.Power} with the glass sized, {free.Power} without");
    }

    /// <summary>A sensor given no size is the image circle: where the lens images its largest field.</summary>
    [Fact]
    public void ASensorWithNoSizeIsTheImageCircle()
    {
        var lens = Bare();
        var r = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0, 5.0 } });
        var chief = RealRayTrace.TraceRecord(lens, r.NominalIndices.ToArray(), r.Nominal, 5.0, 0.0, 0.0, atParaxialFocus: false)[^1];
        Assert.True(r.Sensor.IsImageCircle);
        Assert.Equal(Math.Abs(chief.Y), r.Sensor.Radius!.Value, 12);
        Assert.True(r.Sensor.Covers(0.0, 0.99 * r.Sensor.Radius.Value));
        Assert.False(r.Sensor.Covers(0.0, 1.01 * r.Sensor.Radius.Value));
        // The sensor ghost G5,4 lands 1.9 times as far out as the image: at 5 degrees, off the sensor.
        Assert.Equal(0.0, r.Find(5, 4)!.Fields.Single(f => f.Field == 5.0).Transmitted);
        Assert.True(r.Find(5, 4)!.Fields.Single(f => f.Field == 0.0).Transmitted > 0);
    }

    /// <summary>A size given is the size, and a lens with no field has no image circle to bound the sensor by.</summary>
    [Fact]
    public void AGivenSensorOrNoFieldLeavesTheSensorAsItIs()
    {
        var given = GhostAnalyzer.Analyze(Bare(), Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 }, Sensor = new Sensor { Width = 6, Height = 4 } });
        Assert.False(given.Sensor.IsImageCircle);
        Assert.Equal(6.0, given.Sensor.Width);
        Assert.Null(given.Sensor.Radius);

        var noField = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 } });
        Assert.False(noField.Sensor.Bounded);
    }
}
