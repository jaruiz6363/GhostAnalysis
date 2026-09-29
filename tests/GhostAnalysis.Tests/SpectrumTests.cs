using AberrationCalculator.Core.Models;
using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>Ghosts over several wavelengths: each wavelength its own, and their light added up.</summary>
public class SpectrumTests
{
    private const double F = 0.4861327, D = 0.5875618, Cl = 0.6562725;

    private static readonly Lazy<GhostSpectrum> FdC = new(() =>
        GhostSpectrum.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions
        {
            Fields = new[] { 0.0, 1.0 },
            Wavelengths = new[] { (F, 1.0), (D, 2.0), (Cl, 1.0) },
        }));

    /// <summary>With one wavelength the spectrum is the single analysis.</summary>
    [Fact]
    public void OneWavelengthIsTheSingleAnalysis()
    {
        var lens = Lenses.BiconvexPlate();
        var options = new GhostOptions { Fields = new[] { 0.0, 1.0 } };
        var single = GhostAnalyzer.Analyze(lens, Lenses.Catalog, options);
        var s = GhostSpectrum.Analyze(lens, Lenses.Catalog, options);
        Assert.Single(s.Results);
        foreach (var g in single.Ghosts)
        {
            var sg = s.Find(g.Name)!;
            Assert.Equal(g.PeakIrradiance, sg.PeakIrradiance, 15);
            Assert.Equal(1.0, sg.Shares.Single(), 12);
        }
    }

    /// <summary>
    /// The analysis at a wavelength is the lens analysed with that as its only wavelength: the
    /// wavelength reaches every index, reflectance and ray.
    /// </summary>
    [Fact]
    public void EachWavelengthIsTheLensAtIt()
    {
        var lens = Lenses.BiconvexPlate();
        lens.Wavelengths.Clear();
        lens.Wavelengths.Add(new Wavelength(F, 1.0, true));
        var alone = GhostAnalyzer.Analyze(lens, Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0, 1.0 } });
        var atF = FdC.Value.Results[0];
        Assert.Equal(F, atF.Wavelength);
        foreach (var g in alone.Ghosts)
        {
            var h = atF.Find(g.Path.Surfaces.ToArray())!;
            Assert.Equal(g.Efl, h.Efl, 12);
            Assert.Equal(g.DeltaZ, h.DeltaZ, 12);
            Assert.Equal(g.Transmittance, h.Transmittance, 15);
            Assert.Equal(g.PeakIrradiance, h.PeakIrradiance, 12);
        }
    }

    /// <summary>
    /// BK7 disperses: its index, and so an uncoated surface's reflectance, falls from blue to red,
    /// and the plate ghost G4,3 - the lens's own focus, folded - focuses further off in the blue.
    /// </summary>
    [Fact]
    public void TheGlassDisperses()
    {
        var r = FdC.Value.Results;
        Assert.True(r[0].Reflectance[1] > r[1].Reflectance[1] && r[1].Reflectance[1] > r[2].Reflectance[1]);
        var dz = r.Select(x => x.Find(4, 3)!.DeltaZ).ToList();
        Assert.True(dz[0] > dz[1] && dz[1] > dz[2], string.Join(", ", dz));
        // A positive lens is shorter in the blue: its own focus, and the ghost's, move with it.
        Assert.True(r[0].Nominal.Efl < r[2].Nominal.Efl);
    }

    /// <summary>The light added up is each wavelength's weighted by its share of the light entering.</summary>
    [Fact]
    public void TheLightIsTheWeightedSum()
    {
        var s = FdC.Value;
        foreach (var g in s.Ghosts)
        {
            for (int j = 0; j < g.Fields.Count; j++)
            {
                double expected = (1 * g.IrradianceByWavelength[0][j] + 2 * g.IrradianceByWavelength[1][j] + 1 * g.IrradianceByWavelength[2][j]) / 4;
                Assert.Equal(expected, g.Irradiance[j], 15);
            }
            Assert.Equal(1.0, g.Shares.Sum(), 12);
        }
        Assert.Equal(1, s.Primary);
    }

    /// <summary>
    /// A field one wavelength found special is traced at the others too, not interpolated: the
    /// value there is the other wavelength's own ghost, traced at it.
    /// </summary>
    [Fact]
    public void ASpecialFieldIsTracedAtEveryWavelength()
    {
        var lens = Lenses.BiconvexPlate();
        lens.Surfaces[4].Thickness = 43.0;         // the plate ghost then focuses off axis
        var s = GhostSpectrum.Analyze(lens, Lenses.Catalog, new GhostOptions
        {
            ImageReflects = false, Fields = Enumerable.Range(0, 21).Select(i => i * 0.5).ToArray(),
            Wavelengths = new[] { (F, 1.0), (Cl, 1.0) },
        });
        var g = s.Find("G4,3")!;
        int checkedFields = 0;
        for (int own = 0; own < 2; own++)
        {
            int other = 1 - own;
            var special = g.PerWavelength[own]!.Fields.Where(f => f.Marker != null).Select(f => f.Field)
                           .Except(g.PerWavelength[other]!.Fields.Select(f => f.Field)).ToList();
            foreach (double field in special)
            {
                int j = g.Fields.ToList().IndexOf(field);
                Assert.Equal(g.PerWavelength[other]!.IrradianceAt(field), g.IrradianceByWavelength[other][j], 15);
                Assert.True(g.IrradianceByWavelength[other][j] > 0);
                checkedFields++;
            }
        }
        Assert.True(checkedFields > 0);
    }

    /// <summary>The grating turns red further than blue: an order's displacement goes as the wavelength.</summary>
    [Fact]
    public void AnOrderSpreadsWithTheWavelength()
    {
        var s = GhostSpectrum.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions
        {
            Fields = new[] { 0.0 }, Wavelengths = new[] { (F, 1.0), (Cl, 1.0) },
            Sensor = new Sensor { PeriodX = 8, MaxOrder = 1 },
        });
        var g = s.Find("G5,4 (0,+1)")!;
        double blue = g.PerWavelength[0]!.Fields[0].ParaxialCenter, red = g.PerWavelength[1]!.Fields[0].ParaxialCenter;
        Assert.Equal(Cl / F, red / blue, 12);
    }
}
