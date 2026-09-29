using System.Globalization;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>
/// The worked example of Abd El-Maksoud and Sasian, "Paraxial ghost image analysis", Proc. SPIE
/// 7428, 742807 (2009), repeated in Appl. Opt. 50, 2305 (2011): a biconvex lens and a plate,
/// its six two-reflection ghosts, to every digit the paper prints.
///
/// <para>A few printed values are slips, each betrayed by the paper's own other numbers, and
/// are checked against the values those give:</para>
/// <list type="bullet">
/// <item>G2,1's f_E,g, printed 15.447: its BFD and d' (-40.38, -55.825) give 15.445.</item>
/// <item>G2,1's L'_g, printed 23.204: its L'_g,n - ΔZ (108.837 - 85.63) gives 23.207.</item>
/// <item>G4,1's y'_g,n, printed -3.1169 - its D_xp,g: its D_xp, L' and ΔZ give 3.7773.</item>
/// <item>G4,2's BFD, printed -55.5335: its f_E + d' (88.0681 - 143.6015) gives -55.5334.</item>
/// <item>G4,3's f/#, printed 9.1733 - the decimals of its D_xp: f_E/D_ep is 9.7580.</item>
/// <item>Table 2's G3,2 ratio printed 0.02547, among four-decimal values: 0.2547.</item>
/// </list>
/// </summary>
public class MaksoudValidationTests
{
    private static readonly Lazy<GhostResult> Paper = new(() =>
        GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { ImageReflects = false }));

    /// <summary>Equal to the digits printed: within one unit of the last decimal, for rounding.</summary>
    private static void Printed(string printed, double actual, string what)
    {
        double expected = double.Parse(printed, CultureInfo.InvariantCulture);
        int dot = printed.IndexOf('.');
        int decimals = dot < 0 ? 0 : printed.Length - dot - 1;
        double unit = Math.Pow(10, -decimals);
        Assert.True(Math.Abs(actual - expected) <= unit * 1.01, $"{what}: {actual} against the paper's {printed}");
    }

    [Fact]
    public void TheLensHasTheSixGhostsThePaperLists()
    {
        Assert.Equal(new[] { "G2,1", "G3,1", "G3,2", "G4,1", "G4,2", "G4,3" }, Paper.Value.Ghosts.Select(g => g.Name));
    }

    /// <summary>2009 Table 5: f_E,g, BFD_g, d'_g and y'_g,n.</summary>
    [Theory]
    [InlineData(2, 1, "15.4447", "-40.38", "-55.825", "-27.7216")]     // f_E printed 15.447
    [InlineData(3, 1, "138.1156", "-64.5371", "-202.6527", "-3.9745")]
    [InlineData(3, 2, "90.2153", "-54.4331", "-144.6485", "-5.5247")]
    [InlineData(4, 1, "149.099", "-67.3887", "-216.4877", "-3.7773")]   // y' printed -3.1169
    [InlineData(4, 2, "88.0681", "-55.5334", "-143.6015", "-5.7219")]   // BFD printed -55.5335
    [InlineData(4, 3, "97.5804", "43.9402", "-53.6402", "-0.0671")]
    public void Table5(int k1, int k2, string efl, string bfd, string rearPrincipal, string marginal)
    {
        var g = Paper.Value.Find(k1, k2)!;
        Printed(efl, g.Efl, $"{g.Name} f_E");
        Printed(bfd, g.Bfd, $"{g.Name} BFD");
        Printed(rearPrincipal, g.RearPrincipalPlane, $"{g.Name} d'");
        Printed(marginal, g.MarginalAtImage, $"{g.Name} y'");
    }

    /// <summary>2009 Table 6: D_ep,g, L'_g, ΔZ_g,n, L'_g,n, D_xp,g and f/#_g.</summary>
    [Theory]
    [InlineData(2, 1, "10", "23.2064", "85.63", "108.837", "15.0255", "1.5445")]
    [InlineData(3, 1, "10", "-43.7472", "109.7871", "66.0399", "3.1674", "13.8116")]
    [InlineData(3, 2, "10", "29.9239", "99.6831", "129.6071", "3.3169", "9.0215")]
    [InlineData(4, 1, "10", "-46.4730", "112.6387", "66.1657", "3.1169", "14.9099")]
    [InlineData(4, 2, "10", "28.9627", "100.7834", "129.7461", "3.2887", "8.8068")]
    [InlineData(4, 3, "10", "99.2716", "1.3098", "100.5814", "10.1733", "9.7580")]   // f/# printed 9.1733
    public void Table6(int k1, int k2, string dep, string exitToGhost, string deltaZ, string exitToImage,
                       string dxp, string fno)
    {
        var g = Paper.Value.Find(k1, k2)!;
        Printed(dep, g.EntrancePupilDiameter, $"{g.Name} D_ep");
        Printed(exitToGhost, g.ExitPupilToGhostImage, $"{g.Name} L'_g");
        Printed(deltaZ, g.DeltaZ, $"{g.Name} ΔZ");
        Printed(exitToImage, g.ExitPupilToImage, $"{g.Name} L'_g,n");
        Printed(dxp, g.ExitPupilDiameter, $"{g.Name} D_xp");
        Printed(fno, g.FNumber, $"{g.Name} f/#");
    }

    /// <summary>
    /// The ghost's disc at the image is its cone from the exit pupil, cut there:
    /// |y'_g,n| = (D_xp/2) |ΔZ| / |L'_g|. This is what shows the paper's G4,1 y' to be a slip.
    /// </summary>
    [Fact]
    public void TheGhostsDiscIsItsConeFromTheExitPupil()
    {
        foreach (var g in Paper.Value.Ghosts)
        {
            double cone = 0.5 * g.ExitPupilDiameter * Math.Abs(g.DeltaZ) / Math.Abs(g.ExitPupilToGhostImage);
            Assert.Equal(cone, Math.Abs(g.MarginalAtImage), 9);
        }
    }

    [Fact]
    public void EveryGhostIsStoppedByTheLensStopWhenThePlateIsLarge()
    {
        foreach (var g in Paper.Value.Ghosts)
        {
            Assert.Equal(1, g.StopSurface);
            Assert.False(g.Anomalous);
        }
    }

    /// <summary>
    /// 2009 Table 3: with the plate at its original size the ghosts are stopped elsewhere.
    /// Surfaces are numbered along the unfolded layout, as the paper numbers them.
    /// </summary>
    [Theory]
    [InlineData(2, 1, 6)]
    [InlineData(3, 1, 8)]
    [InlineData(3, 2, 6)]
    [InlineData(4, 1, 3)]
    [InlineData(4, 2, 8)]
    [InlineData(4, 3, 3)]
    public void Table3TheGhostStopsOfTheOriginalLens(int k1, int k2, int layoutStop)
    {
        var g = Original.Value.Find(k1, k2)!;
        Assert.Equal(layoutStop, g.StopLayoutSurface);
        Assert.True(g.Anomalous);
    }

    /// <summary>2009 Table 2: how far the potential marginal ray fills each layout surface.</summary>
    [Theory]
    [InlineData(2, 1, "0.2", "0.1966", "0.1735", "0.1331", "1.0991", "1.13312")]
    [InlineData(3, 1, "0.2", "0.1966", "0.2011", "0.0084", "0.0150", "0.0201", "0.1976", "0.2025")]
    [InlineData(3, 2, "0.2", "0.1966", "0.2011", "0.0084", "0.2547", "0.2615")]      // printed 0.02547
    [InlineData(4, 1, "0.2", "0.1966", "0.2011", "0.2010", "0.1953", "0.0111", "0.0176", "0.0224", "0.1913", "0.1959")]
    [InlineData(4, 2, "0.2", "0.1966", "0.2011", "0.2010", "0.1953", "0.0111", "0.2663", "0.2733")]
    [InlineData(4, 3, "0.2", "0.1966", "0.2011", "0.2010", "0.1953", "0.1952")]
    public void Table2(int k1, int k2, params string[] ratios)
    {
        var g = Original.Value.Find(k1, k2)!;
        Assert.Equal(ratios.Length + 2, g.StopRatios.Count);     // and the object and the image
        for (int j = 0; j < ratios.Length; j++)
            Printed(ratios[j], g.StopRatios[j + 1], $"{g.Name} surface {j + 1}");
    }

    /// <summary>
    /// The lens as the 2009 paper first gives it (its Table 1), with the semi-diameters its
    /// Table 2 ratios were computed with: 5 for both faces of the lens (Table 1 prints 4.92 for
    /// the back, but its row 2 is the ray's 0.98296 over 5), 2.34 for the plate's front, and
    /// 2.30735 for its back (Table 1 prints 2.31, rounded: G2,1's 1.13312, to five places, puts
    /// it at 2.30735, and G3,1's 0.2025 and the G4 ghosts' 0.2010 agree).
    /// </summary>
    private static readonly Lazy<GhostResult> Original = new(() =>
    {
        var lens = Lenses.BiconvexPlate();
        lens.Surfaces[3].SemiDiameter = 2.34;
        lens.Surfaces[4].SemiDiameter = 2.30735;
        return GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { ImageReflects = false });
    });

    /// <summary>
    /// 2011, section 11 and Fig. 8: 1 W entering, the lens and plate uncoated. Each ghost
    /// reflects twice and crosses a surface every other time it meets one - four times for
    /// G4,3, eight for G4,1, which goes back through the whole lens - so carries T^c R^2 of the
    /// watt. G4,3, the plate's own ghost, is nearly focused and is the bright one, about 0.11
    /// W/mm^2 (Fig. 8f, 16b); G2,1 is spread over a disc 55 mm across, about 6e-7 W/mm^2 (Fig. 8b).
    /// </summary>
    [Theory]
    [InlineData(2, 1, 4)]
    [InlineData(3, 1, 6)]
    [InlineData(3, 2, 4)]
    [InlineData(4, 1, 8)]
    [InlineData(4, 2, 6)]
    [InlineData(4, 3, 4)]
    public void Fig8TheTransmittances(int k1, int k2, int crossings)
    {
        double n = 1.5168, rr = Math.Pow((n - 1) / (n + 1), 2), tt = 1 - rr;
        Assert.Equal(Math.Pow(tt, crossings) * rr * rr, Paper.Value.Find(k1, k2)!.Transmittance, 8);
    }

    [Fact]
    public void Fig8TheIrradiances()
    {
        var r = Paper.Value;
        foreach (var g in r.Ghosts)
        {
            Assert.Equal(g.Transmittance / (Math.PI * g.MarginalAtImage * g.MarginalAtImage), g.Irradiance, 12);
        }
        Assert.InRange(r.Find(4, 3)!.Irradiance, 0.10, 0.12);
        Assert.InRange(r.Find(2, 1)!.Irradiance, 5.5e-7, 7e-7);
        Assert.Equal("G4,3", r.Ranked.First().Name);
    }
}
