using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// One ghost across the spectrum: itself at each wavelength, and its light added up over them.
/// </summary>
public sealed class SpectralGhost
{
    /// <summary>The ghost's name, the same at every wavelength: its reflections and its orders at the sensor.</summary>
    public required string Name { get; init; }

    /// <summary>The ghost at each wavelength, in the spectrum's order; null where it does not exist - an order that does not propagate there.</summary>
    public required IReadOnlyList<Ghost?> PerWavelength { get; init; }

    /// <summary>The ghost at the primary wavelength, or where it is not there, at the first where it is.</summary>
    public required Ghost Primary { get; init; }

    /// <summary>The fields its light is added up at, from the axis out.</summary>
    public required IReadOnlyList<double> Fields { get; init; }

    /// <summary>
    /// Its irradiance at each of those fields over the whole spectrum: each wavelength's weighted
    /// by the share of the light entering at it.
    /// </summary>
    public required IReadOnlyList<double> Irradiance { get; init; }

    /// <summary>At each wavelength, its irradiance at each of the fields.</summary>
    public required IReadOnlyList<IReadOnlyList<double>> IrradianceByWavelength { get; init; }

    public double PeakIrradiance => Irradiance.Count == 0 ? 0.0 : Irradiance.Max();

    /// <summary>The field where it is brightest over the spectrum.</summary>
    public double PeakField => Irradiance.Count == 0 ? 0.0 : Fields[Irradiance.ToList().IndexOf(PeakIrradiance)];

    /// <summary>
    /// Its colour at its brightest field: the share of its irradiance there each wavelength
    /// brings, weights included. They add up to 1.
    /// </summary>
    public required IReadOnlyList<double> Shares { get; init; }
}

/// <summary>
/// Every ghost of a lens, at each of several wavelengths, and added up over them.
///
/// <para>The lens is analysed at each wavelength as it is at one (<see cref="GhostAnalyzer"/>):
/// its glasses' indices, and so each ghost's focus, size and place on the sensor, change with
/// the wavelength - a ghost's colour - and so do the surfaces' Fresnel reflectances, the angles
/// the sensor's grating turns the light by, and the Airy disc. Each ghost's light is then added
/// up over the spectrum, each wavelength weighted by the share of the light entering at it.</para>
///
/// <para>The fields it is added up at are the sweep's and every field any wavelength found
/// special - where its ghost is brightest, where its focus crosses the sensor - each traced at
/// every wavelength, so the sum at a sharp off-axis peak is traced, not interpolated.</para>
/// </summary>
public sealed class GhostSpectrum
{
    /// <summary>The wavelengths, in micrometres, and their weights.</summary>
    public required IReadOnlyList<(double Um, double Weight)> Wavelengths { get; init; }

    /// <summary>The analysis at each wavelength, in the same order.</summary>
    public required IReadOnlyList<GhostResult> Results { get; init; }

    /// <summary>Which of them is at the primary wavelength.</summary>
    public required int Primary { get; init; }

    public GhostResult PrimaryResult => Results[Primary];

    /// <summary>The ghosts, each across the spectrum.</summary>
    public required IReadOnlyList<SpectralGhost> Ghosts { get; init; }

    /// <summary>The ghosts, brightest over the spectrum at their worst field first.</summary>
    public IEnumerable<SpectralGhost> Ranked => Ghosts.OrderByDescending(g => g.PeakIrradiance);

    public SpectralGhost? Find(string name) => Ghosts.FirstOrDefault(g => g.Name == name);

    public static GhostSpectrum Analyze(OpticalSystem lens, GlassCatalog catalog, GhostOptions? options = null)
    {
        if (lens == null) throw new ArgumentNullException(nameof(lens));
        options ??= new GhostOptions();
        if (lens.Wavelengths.Count == 0) throw new InvalidOperationException("The lens has no wavelengths.");

        var waves = (options.Wavelengths ?? lens.Wavelengths.Select(w => (w.Value, w.Weight)).ToList()).ToList();
        if (waves.Count == 0) throw new ArgumentException("No wavelengths to analyse at.", nameof(options));
        if (waves.Any(w => w.Um <= 0 || w.Weight < 0))
            throw new ArgumentException("A wavelength is a positive number of micrometres, and its weight not negative.", nameof(options));
        double primaryUm = lens.Wavelengths[Math.Max(0, lens.PrimaryWavelengthIndex)].Value;
        int primary = waves.Select((w, i) => (Math.Abs(w.Um - primaryUm), i)).Min().i;

        var results = waves.Select(w => GhostAnalyzer.Analyze(lens, catalog, options.At(w.Um))).ToList();
        double total = waves.Sum(w => w.Weight);
        if (total <= 0) total = 1.0;

        var names = results.SelectMany(r => r.Ghosts.Select(g => g.Name)).Distinct().ToList();
        var ghosts = new SpectralGhost[names.Count];
        Parallel.For(0, names.Count, k =>
        {
            string name = names[k];
            var per = results.Select(r => r.Ghosts.FirstOrDefault(g => g.Name == name)).ToList();

            // The fields to add up at: the sweep's, and each wavelength's own special ones.
            var fields = per.Where(g => g != null).SelectMany(g => g!.Fields.Select(f => f.Field))
                            .Distinct().OrderBy(f => f).ToList();
            var byWave = new List<IReadOnlyList<double>>();
            for (int i = 0; i < per.Count; i++)
            {
                var g = per[i];
                byWave.Add(fields.Select(f =>
                {
                    if (g == null) return 0.0;
                    var at = g.Fields.FirstOrDefault(x => x.Field == f);
                    return at != null ? at.Brightness : options.RealRays ? g.IrradianceAt(f) : 0.0;
                }).ToList());
            }
            var sum = fields.Select((_, j) => Enumerable.Range(0, per.Count).Sum(i => waves[i].Weight * byWave[i][j]) / total).ToList();

            int peak = sum.Count == 0 ? 0 : sum.IndexOf(sum.Max());
            double atPeak = sum.Count == 0 ? 0.0 : sum[peak] * total;
            var shares = Enumerable.Range(0, per.Count)
                                   .Select(i => atPeak > 0 && fields.Count > 0 ? waves[i].Weight * byWave[i][peak] / atPeak : 0.0).ToList();

            ghosts[k] = new SpectralGhost
            {
                Name = name,
                PerWavelength = per,
                Primary = per[primary] ?? per.First(g => g != null)!,
                Fields = fields,
                Irradiance = sum,
                IrradianceByWavelength = byWave,
                Shares = shares,
            };
        });

        return new GhostSpectrum { Wavelengths = waves, Results = results, Primary = primary, Ghosts = ghosts };
    }
}
