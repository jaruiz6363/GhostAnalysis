using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>The sensor's edges, and the grating its pixels make.</summary>
public class SensorTests
{
    private const double Lambda = 0.5875618;

    private static GhostResult Run(Sensor sensor, double[] fields, int pupil = 21, int reflections = 2) =>
        GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions
        {
            Sensor = sensor, Fields = fields, PupilSamples = pupil, Reflections = reflections,
        });

    // ── Efficiencies ─────────────────────────────────────────────────────────────

    /// <summary>A pixel filling its whole period is a plain mirror: every order but the zeroth vanishes.</summary>
    [Fact]
    public void AFullPixelIsAMirror()
    {
        var orders = new Sensor { PeriodX = 8, FillFactor = 1.0 }.Orders(Lambda, 1.0);
        Assert.Equal(new[] { (0, 0) }, orders.Select(o => (o.M, o.N)));
        Assert.Equal(1.0, orders[0].Efficiency, 12);
    }

    /// <summary>The shares of every order that propagates add up to all the reflected light.</summary>
    [Fact]
    public void TheOrdersShareAllTheLight()
    {
        var sensor = new Sensor { PeriodX = 8, FillFactor = 0.5, MaxOrder = 100, MinimumEfficiency = 0 };
        var orders = sensor.Orders(Lambda, 1.0);
        Assert.Equal(1.0, orders.Sum(o => o.Efficiency), 12);
        // Symmetric, and falling away from the zeroth.
        var e = orders.ToDictionary(o => (o.M, o.N), o => o.Efficiency);
        Assert.Equal(e[(1, 0)], e[(-1, 0)], 15);
        Assert.Equal(e[(1, 0)], e[(0, 1)], 15);
        Assert.True(e[(0, 0)] > e[(1, 0)] && e[(1, 0)] > e[(1, 1)]);
        // Only the orders the grating equation lets out: 8 / 0.588 = 13.6, so up to 13.
        Assert.Equal(13, orders.Max(o => o.M));
    }

    /// <summary>A period shorter than the wavelength sends nothing into any order but the zeroth.</summary>
    [Fact]
    public void AFineGratingOnlyReflects()
    {
        Assert.Equal(new[] { (0, 0) }, new Sensor { PeriodX = 0.5 }.Orders(Lambda, 1.0).Select(o => (o.M, o.N)));
    }

    [Fact]
    public void MeasuredEfficienciesReplaceTheModel()
    {
        var sensor = new Sensor
        {
            PeriodX = 8,
            Efficiencies = new Dictionary<(int, int), double> { [(0, 0)] = 0.7, [(1, 0)] = 0.05, [(0, 5)] = 0.0001 },
        };
        var orders = sensor.Orders(Lambda, 1.0);
        Assert.Equal(new[] { (0, 0, 0.7), (1, 0, 0.05) }, orders);         // (0,5) is beyond MaxOrder
    }

    // ── The grating, traced ──────────────────────────────────────────────────────

    /// <summary>
    /// G5,4 in order (0,+1): off the sensor turned by λ/Λ, back to the plate's flat rear face
    /// 45.25 away and forward again - two flats, so its chief ray lands 2 × 45.25 × tan of the
    /// order's angle from where the zeroth order's does, on axis: from the axis. Paraxially, the
    /// angle itself.
    /// </summary>
    [Fact]
    public void AnOrderLandsWhereTheGratingEquationSendsIt()
    {
        var r = Run(new Sensor { PeriodX = 8 }, new[] { 0.0 });
        double k = Lambda / 8.0;
        double tan = k / Math.Sqrt(1 - k * k);

        var up = r.Find(new[] { 5, 4 }, (0, 1))!.Fields[0];
        Assert.Equal(2 * 45.25 * tan, up.ChiefY, 9);
        Assert.Equal(2 * 45.25 * k, up.ParaxialCenter, 9);
        Assert.Equal(0.0, up.CentroidX, 9);

        var side = r.Find(new[] { 5, 4 }, (1, 0))!.Fields[0];
        Assert.Equal(2 * 45.25 * k, side.ParaxialCenterX, 9);
        Assert.InRange(side.CentroidX / (2 * 45.25 * tan), 0.995, 1.005);
        Assert.Equal(0.0, side.CentroidY, 9);

        // The opposite order the opposite way.
        var down = r.Find(new[] { 5, 4 }, (0, -1))!.Fields[0];
        Assert.Equal(-up.ChiefY, down.ChiefY, 9);
    }

    /// <summary>Each order carries its share of what the sensor reflects, and no more.</summary>
    [Fact]
    public void AnOrderCarriesItsShare()
    {
        var sensor = new Sensor { PeriodX = 8 };
        var plain = Run(new Sensor(), new[] { 0.0 }).Find(5, 4)!;
        var r = Run(sensor, new[] { 0.0 });
        foreach (var (m, n, e) in sensor.Orders(Lambda, 1.0))
            Assert.Equal(plain.Transmittance * e, r.Find(new[] { 5, 4 }, (m, n))!.Transmittance, 15);
        // A ghost that does not reflect from the sensor is one ghost, with no order.
        Assert.Empty(r.Find(4, 3)!.Orders);
        Assert.Single(r.Ghosts, g => g.Path.ToString() == "G4,3");
    }

    /// <summary>Two reflections from the sensor, two orders: a four-reflection ghost diffracts twice.</summary>
    [Fact]
    public void TwoReflectionsFromTheSensorDiffractTwice()
    {
        var sensor = new Sensor { PeriodX = 8, MaxOrder = 1 };
        var r = Run(sensor, new[] { 0.0 }, reflections: 4);
        var g = r.Find(new[] { 5, 4, 5, 4 }, (0, 1), (0, 1))!;
        double k = Lambda / 8.0;
        // Each kick carried twice across the 45.25 gap and back: four crossings, then two.
        Assert.Equal(2 * 45.25 * k * 2 + 2 * 45.25 * k, g.Fields[0].ParaxialCenter, 9);
        var e = sensor.Orders(Lambda, 1.0).Single(o => (o.M, o.N) == (0, 1)).Efficiency;
        Assert.Equal(e * e, g.OrderEfficiency, 15);
    }

    // ── The sensor's edges ───────────────────────────────────────────────────────

    /// <summary>
    /// G3,2 spreads its light evenly over a disc 5.5 in radius; a 4 × 4 sensor at its centre sees
    /// the part of it the square covers, 16 / (π 5.5²) of it.
    /// </summary>
    [Fact]
    public void ASmallSensorSeesPartOfTheGhost()
    {
        var r = Run(new Sensor { Width = 4, Height = 4 }, new[] { 0.0 }, pupil: 41);
        var g = r.Find(3, 2)!;
        double radius = Math.Abs(g.MarginalAtImage);
        Assert.InRange(g.Fields[0].Transmitted / (16.0 / (Math.PI * radius * radius)), 0.9, 1.1);

        var all = Run(new Sensor(), new[] { 0.0 }, pupil: 41).Find(3, 2)!;
        Assert.Equal(1.0, all.Fields[0].Transmitted, 9);
    }

    /// <summary>
    /// A sensor ghost begins where the image falls: off the sensor there is nothing to reflect
    /// it. With a 4 × 4 sensor the image is on it at 1 degree (1.7 from the centre) and off it at 2 (3.4).
    /// </summary>
    [Fact]
    public void TheSensorReflectsOnlyWhereItIs()
    {
        var r = Run(new Sensor { Width = 4, Height = 4 }, new[] { 1.0, 2.0 });
        var g = r.Find(5, 4)!;
        Assert.True(g.Fields.Single(f => f.Field == 1.0).Transmitted > 0);
        Assert.Equal(0.0, g.Fields.Single(f => f.Field == 2.0).Transmitted);
        Assert.True(r.Find(4, 3)!.Fields.Single(f => f.Field == 1.0).Transmitted > 0);   // the plate ghost lands at 1.73
        Assert.Equal(0.0, r.Find(4, 3)!.Fields.Single(f => f.Field == 2.0).Transmitted); // and at 3.46, off the edge
    }
}
