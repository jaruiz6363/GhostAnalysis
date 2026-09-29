using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.RayTrace;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>Ghosts off axis: where they land, how large they are, and what cuts them off.</summary>
public class OffAxisTests
{
    private static readonly double[] Degrees = { 0, 0.5, 1, 2, 2.5, 3.2, 5 };

    private static readonly Lazy<GhostResult> Swept = new(() =>
        GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { Fields = Degrees }));

    private static GhostField At(Ghost g, double field) => g.Fields.Single(f => f.Field == field);

    /// <summary>Paraxially a ghost lands at a fixed multiple of the image height, at every field.</summary>
    [Fact]
    public void AGhostLandsAtAFixedMultipleOfTheImage()
    {
        foreach (var g in Swept.Value.Ghosts)
            foreach (var f in g.Fields.Where(f => f.Field > 0))
                Assert.Equal(g.Magnification, f.ParaxialCenter / f.ImageHeight, 9);
    }

    /// <summary>
    /// G5,4, off the sensor and back off the plate's rear face: two flats 45.25 apart, so the
    /// ghost's chief ray is the lens's own, carried back and forth once more - it lands at the
    /// image height plus 2 x 45.25 times the chief ray's slope there. Worked from the lens's
    /// own trace, with no ghost layout: paraxially, and by real rays.
    /// </summary>
    [Fact]
    public void TheSensorGhostIsTheImageCarriedTwiceAcrossTheGap()
    {
        var lens = Lenses.BiconvexPlate();
        var n = IndexResolver.Build(lens, Lenses.Catalog, 0.5875618);
        var g = Swept.Value.Find(5, 4)!;
        foreach (double h in Degrees.Where(d => d > 0))
        {
            var p = ParaxialTrace.Trace(lens, n, h);
            int last = lens.Surfaces.Count - 2;
            Assert.Equal(p.Ybar[^1] + 2 * 45.25 * p.Ubar[last], At(g, h).ParaxialCenter, 9);

            var hits = RealRayTrace.TraceRecord(lens, n, ParaxialTrace.Trace(lens, n, 0.0), h, 0.0, 0.0, atParaxialFocus: false);
            var end = hits[^1];
            Assert.Equal(end.Y + 2 * 45.25 * end.M / end.N, At(g, h).ChiefY, 9);
        }
    }

    /// <summary>
    /// A slow ghost, badly out of focus, is an evenly filled disc: its real spot has the
    /// paraxial disc's radius over √2 for its RMS, and sits where the paraxial chief ray puts it.
    /// </summary>
    [Theory]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    public void ADefocusedGhostIsTheParaxialDisc(int k1, int k2)
    {
        var g = Swept.Value.Find(k1, k2)!;
        var axis = At(g, 0);
        Assert.Equal(0.0, axis.CentroidY, 9);
        Assert.Equal(0.0, axis.CentroidX, 9);
        Assert.InRange(axis.RmsRadius / (axis.ParaxialRadius / Math.Sqrt(2)), 0.98, 1.02);
        Assert.Equal(1.0, axis.Transmitted);

        var near = At(g, 0.5);
        Assert.InRange(near.CentroidY / near.ParaxialCenter, 0.99, 1.01);
    }

    /// <summary>
    /// G4,1 goes back through the lens to its front face; off axis its beam walks off the
    /// lens's edge, and the real rays and the paraxial discs agree on where: nearly all through
    /// at 2.5 degrees, none at 3.2.
    /// </summary>
    [Fact]
    public void RealAndParaxialVignettingAgree()
    {
        var g = Swept.Value.Find(4, 1)!;
        Assert.InRange(At(g, 2.5).Transmitted, 0.95, 1.0);
        Assert.InRange(At(g, 2.5).ParaxialTransmitted, 0.95, 1.0);
        Assert.Equal(0.0, At(g, 3.2).Transmitted);
        Assert.Equal(0.0, At(g, 3.2).ParaxialTransmitted);
        Assert.Equal(0.0, At(g, 3.2).Irradiance);
        // Nothing is cut before the paraxial field stop allows.
        Assert.True(g.UnvignettedField > 1.0 && g.UnvignettedField < 2.0, $"{g.UnvignettedField}");
        Assert.Equal(1.0, At(g, 1).Transmitted);
    }

    [Fact]
    public void TheOtherSideOfTheAxisIsTheMirrorImage()
    {
        var lens = Lenses.BiconvexPlate();
        var r = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = new[] { -2.0, 2.0 } });
        foreach (var g in r.Ghosts)
        {
            GhostField minus = At(g, -2.0), plus = At(g, 2.0);
            Assert.Equal(-plus.CentroidY, minus.CentroidY, 9);
            Assert.Equal(plus.RmsRadius, minus.RmsRadius, 9);
        }
    }

    /// <summary>A lens with fields is swept from the axis to 1.2 times its largest.</summary>
    [Fact]
    public void TheDefaultSweepGoesPastTheLensesField()
    {
        var lens = Lenses.BiconvexPlate();
        lens.Fields.Clear();
        lens.Fields.Add(new AberrationCalculator.Core.Models.Field(0.0));
        lens.Fields.Add(new AberrationCalculator.Core.Models.Field(10.0));
        var fields = GhostAnalyzer.FieldsFor(lens, new GhostOptions());
        Assert.Equal(13, fields.Count);
        Assert.Equal(0.0, fields[0]);
        Assert.Equal(12.0, fields[^1], 12);
        Assert.Equal(new[] { 0.0 }, GhostAnalyzer.FieldsFor(Lenses.BiconvexPlate(), new GhostOptions()));
    }

    [Fact]
    public void AReflectanceIsAFraction()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { ImageReflectance = 10 }));
        Assert.ThrowsAny<ArgumentException>(() =>
            GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { CoatedReflectance = 0.5 * 2.5 }));
    }

    [Theory]
    [InlineData(1, 1, 3, 0)]                 // apart
    [InlineData(1, 3, 1, Math.PI)]           // inside
    [InlineData(1, 1, 0, Math.PI)]           // the same disc
    public void CircleOverlap(double r1, double r2, double d, double area)
    {
        Assert.Equal(area, GhostAnalyzer.CircleOverlap(r1, r2, d), 12);
    }

    /// <summary>Two unit discs a radius apart share 2π/3 - √3/2.</summary>
    [Fact]
    public void CircleOverlapLens()
    {
        Assert.Equal(2 * Math.PI / 3 - Math.Sqrt(3) / 2, GhostAnalyzer.CircleOverlap(1, 1, 1), 12);
    }
}
