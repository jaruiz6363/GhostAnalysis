using AberrationCalculator.Core.Glass;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

public class GhostLayoutTests
{
    /// <summary>
    /// G4,2 of the paper's lens: forward through 1, 2, 3 to the plate's back, back through 3 to
    /// the lens's back, forward through 3 and 4 to the image - the 2009 paper's Table 2 column
    /// of eight surfaces.
    /// </summary>
    [Fact]
    public void TheLayoutIsTheLensRunBackwardsBetweenTheReflections()
    {
        var lens = Lenses.BiconvexPlate();
        var layout = GhostLayout.Build(lens, new GhostPath(4, 2));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 3, 2, 3, 4, 5 }, layout.Origin);
        Assert.Equal(new[] { false, false, false, false, true, false, true, false, false, false }, layout.Reflects);

        var s = layout.System.Surfaces;
        Assert.Equal("MIRROR", s[4].Material);
        Assert.Equal("MIRROR", s[6].Material);
        // Backwards the thicknesses are negative: the mirror at 4 back to 3 through the plate,
        // and 3, crossed backwards, leaves the light in the air between the lens and the plate.
        Assert.Equal(-1.0, s[4].Thickness, 12);
        Assert.Null(s[5].Material);
        Assert.Equal(-50.0, s[5].Thickness, 12);
        Assert.Equal(50.0, s[6].Thickness, 12);
        Assert.Equal("N-BK7", s[7].Material);
        Assert.Equal(45.25, s[8].Thickness, 12);
        Assert.All(s, x => Assert.False(x.IsStop));
    }

    /// <summary>A layout crossing glass backwards is in the glass: the index there is the glass's.</summary>
    [Fact]
    public void BackwardsThroughTheLensIsInItsGlass()
    {
        var lens = Lenses.BiconvexPlate();
        var layout = GhostLayout.Build(lens, new GhostPath(3, 1));
        // 1, 2, 3(mirror), 2 backwards, 1(mirror), 2, 3, 4, image
        Assert.Equal(new[] { 0, 1, 2, 3, 2, 1, 2, 3, 4, 5 }, layout.Origin);
        var n = IndexResolver.Build(layout.System, Lenses.Catalog, 0.5875618);
        Assert.Equal(1.0, n[3], 12);               // the mirror, in the air it arrived in
        Assert.Equal(n[1], n[4], 12);              // 2 crossed backwards: into the lens's glass
        Assert.Equal(n[1], n[5], 12);              // the mirror at 1, in that glass
        Assert.True(n[1] > 1.5);
    }

    /// <summary>A sensor ghost: to the image, back to a surface, and to the image again.</summary>
    [Fact]
    public void TheSensorReflects()
    {
        var lens = Lenses.BiconvexPlate();
        var layout = GhostLayout.Build(lens, new GhostPath(5, 4));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 4, 5 }, layout.Origin);
        Assert.Equal(-45.25, layout.System.Surfaces[5].Thickness, 12);
        Assert.Equal(45.25, layout.System.Surfaces[6].Thickness, 12);
    }
}
