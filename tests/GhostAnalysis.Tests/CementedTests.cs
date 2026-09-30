using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>
/// A cemented surface - glass on both sides, a doublet's inner surface - does not reflect: the
/// cement takes up the index step. Asked to, it reflects Fresnel's value between the two glasses.
/// </summary>
public class CementedTests
{
    /// <summary>The papers' lens with N-SF5 from its back face to the plate: surfaces 2 and 3 cemented.</summary>
    private static AberrationCalculator.Core.Models.OpticalSystem Cemented()
    {
        var lens = Lenses.BiconvexPlate();
        lens.Surfaces[2].Material = "N-SF5";
        return lens;
    }

    private static readonly int[] Joints = { 2, 3 };

    private static IEnumerable<int> SurfacesOf(Ghost g) =>
        g.Name.TrimStart('G').Split(' ')[0].Split(',').Select(int.Parse);

    [Fact]
    public void ACementedSurfaceSendsNothingBack()
    {
        var r = GhostAnalyzer.Analyze(Cemented(), Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0 }, RealRays = false });
        foreach (int k in Joints) Assert.Equal(0.0, r.Reflectance[k]);
        Assert.True(r.Reflectance[1] > 0.0 && r.Reflectance[4] > 0.0);         // glass-air still reflects
        Assert.DoesNotContain(r.Ghosts, g => SurfacesOf(g).Intersect(Joints).Any());
        Assert.Contains(r.Ghosts, g => g.Name == "G4,1");
    }

    [Fact]
    public void AskedToItReflectsFresnelsValue()
    {
        var r = GhostAnalyzer.Analyze(Cemented(), Lenses.Catalog,
            new GhostOptions { Fields = new[] { 0.0 }, RealRays = false, CementedReflects = true, CoatedReflectance = 0.005 });
        var n = AberrationCalculator.Core.Glass.IndexResolver.Build(Cemented(), Lenses.Catalog, r.Wavelength);
        double fresnel = Math.Pow((n[1] - n[2]) / (n[1] + n[2]), 2);
        Assert.Equal(fresnel, r.Reflectance[2], 12);                              // not the coating
        Assert.Contains(r.Ghosts, g => SurfacesOf(g).Contains(2));
    }
}
