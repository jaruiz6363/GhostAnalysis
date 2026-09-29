using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>What to count as a ghost, and how much light the surfaces send back.</summary>
public sealed class GhostOptions
{
    /// <summary>How many reflections a ghost has: 2, 4, ... Each pair more is weaker by about R^2.</summary>
    public int Reflections { get; init; } = 2;

    /// <summary>Whether the image - the sensor - reflects. It does; the papers leave it out.</summary>
    public bool ImageReflects { get; init; } = true;

    /// <summary>
    /// The sensor's reflectance. A lens file does not say, and a sensor is not glass, so Fresnel
    /// cannot: it is a property of the sensor and its cover, to be measured or looked up. The
    /// default is a placeholder of a few per cent.
    /// </summary>
    public double ImageReflectance { get; init; } = 0.05;

    /// <summary>
    /// The reflectance of every glass-air surface, for a coated lens; null for uncoated glass,
    /// whose reflectance is Fresnel's at normal incidence. A cemented surface is always
    /// Fresnel's: the index step there is small, and so is what it sends back.
    /// </summary>
    public double? CoatedReflectance { get; init; }

    /// <summary>Power entering the lens's entrance pupil, in whatever unit the irradiances should come out in per lens unit squared.</summary>
    public double InputPower { get; init; } = 1.0;
}

/// <summary>Every ghost of one lens, analysed at one wavelength.</summary>
public sealed class GhostResult
{
    public required OpticalSystem Lens { get; init; }
    public required GhostOptions Options { get; init; }
    public required double Wavelength { get; init; }

    /// <summary>The lens's own paraxial trace.</summary>
    public required ParaxialResult Nominal { get; init; }

    /// <summary>Each surface's reflectance, indexed like the lens (0 for the object).</summary>
    public required IReadOnlyList<double> Reflectance { get; init; }

    /// <summary>The ghosts, in the order <see cref="GhostPath.Enumerate"/> gives them.</summary>
    public required IReadOnlyList<Ghost> Ghosts { get; init; }

    /// <summary>Glasses the catalogs did not have, traced as air. Empty for a usable result.</summary>
    public required IReadOnlyList<string> Unresolved { get; init; }

    /// <summary>The ghosts, brightest at the image first.</summary>
    public IEnumerable<Ghost> Ranked => Ghosts.OrderByDescending(g => g.Irradiance);

    public Ghost? Find(params int[] surfaces) => Ghosts.FirstOrDefault(g => g.Path.Surfaces.SequenceEqual(surfaces));
}

/// <summary>
/// Paraxial ghost image analysis: Abd El-Maksoud and Sasian, "Paraxial ghost image analysis",
/// Proc. SPIE 7428, 742807 (2009), and "Modeling and analyzing ghost images for incoherent
/// optical systems", Appl. Opt. 50, 2305 (2011).
///
/// <para>Each ghost is unfolded (<see cref="GhostLayout"/>) and traced as a lens of its own.
/// Its aperture stop is found the 2009 paper's way (section 6): an axial ray is traced through
/// the layout, and the surface it fills most - height over semi-diameter - is the stop, which
/// need not be the lens's. The ray scaled to fill that surface exactly is the ghost's marginal
/// ray, and its pupils, focal length, image and size at the lens's image follow from the
/// ordinary paraxial trace.</para>
///
/// <para>Its light is the 2011 paper's (section 9): what enters its entrance pupil, times a
/// Fresnel transmittance at every surface crossed and a reflectance at every reflection, spread
/// evenly over the ghost's disc at the image.</para>
/// </summary>
public static class GhostAnalyzer
{
    public static GhostResult Analyze(OpticalSystem lens, GlassCatalog catalog, GhostOptions? options = null)
    {
        if (lens == null) throw new ArgumentNullException(nameof(lens));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        options ??= new GhostOptions();
        if (lens.Wavelengths.Count == 0) throw new InvalidOperationException("The lens has no wavelengths.");

        double wave = lens.Wavelengths[Math.Max(0, lens.PrimaryWavelengthIndex)].Value;
        var unresolved = new List<string>();
        var n = IndexResolver.Build(lens, catalog, wave, unresolved);
        var nominal = ParaxialTrace.Trace(lens, n, 0.0);

        int image = lens.Surfaces.Count - 1;
        int stop = lens.StopSurfaceIndex;
        if (stop < 1 || stop >= image) stop = image - 1;
        // The lens's stop bounds its beam whatever its file says of its size.
        double stopSd = lens.Surfaces[stop].SemiDiameter > 0 ? lens.Surfaces[stop].SemiDiameter : Math.Abs(nominal.Y[stop]);

        var reflectance = new double[image + 1];
        for (int k = 1; k < image; k++) reflectance[k] = Reflectance(n[k - 1], n[k], options.CoatedReflectance);
        reflectance[image] = options.ImageReflects ? options.ImageReflectance : 0.0;

        bool infinite = double.IsInfinity(lens.Surfaces[0].Thickness) || Math.Abs(lens.Surfaces[0].Thickness) >= 1e12;
        double nominalRay = infinite ? nominal.Y[1] : nominal.U[0];

        var ghosts = new List<Ghost>();
        foreach (var path in GhostPath.Enumerate(image - 1, options.Reflections, options.ImageReflects))
        {
            // A surface with no index step - a dummy, a stop in air - sends nothing back.
            if (path.Surfaces.Any(k => reflectance[k] <= 0.0)) continue;
            ghosts.Add(Analyze(lens, catalog, wave, path, stop, stopSd, infinite, nominalRay, reflectance, options));
        }

        return new GhostResult
        {
            Lens = lens,
            Options = options,
            Wavelength = wave,
            Nominal = nominal,
            Reflectance = reflectance,
            Ghosts = ghosts,
            Unresolved = unresolved.Distinct().ToList(),
        };
    }

    private static Ghost Analyze(OpticalSystem lens, GlassCatalog catalog, double wave, GhostPath path,
                                 int stop, double stopSd, bool infinite, double nominalRay,
                                 double[] reflectance, GhostOptions options)
    {
        var layout = GhostLayout.Build(lens, path);
        var sys = layout.System;
        var n = IndexResolver.Build(sys, catalog, wave);
        int last = sys.Surfaces.Count - 1;

        // The potential marginal ray: from the axial object point, unit height at surface 1
        // (unit slope from a finite object). Where the stop is marked does not change it.
        int firstStop = Enumerable.Range(1, last - 1).First(j => layout.Origin[j] == stop);
        SetStop(sys, firstStop);
        SetAperture(sys, infinite, n[0], 1.0);
        var unit = ParaxialTrace.Trace(sys, n, 0.0);

        var ratios = new double[sys.Surfaces.Count];
        ratios[0] = ratios[last] = double.NaN;
        int ghostStop = firstStop;
        int image = lens.Surfaces.Count - 1;
        for (int j = 1; j < last; j++)
        {
            // The image's semi-diameter in a lens file is the height of the image, not the size
            // of the sensor, so the sensor, reflecting, is taken to catch the whole beam.
            double sd = layout.Origin[j] == stop ? stopSd
                      : layout.Origin[j] == image ? 0.0
                      : sys.Surfaces[j].SemiDiameter;
            ratios[j] = sd > 0 ? Math.Abs(unit.Y[j]) / sd : double.NaN;
            if (!double.IsNaN(ratios[j]) && ratios[j] > ratios[ghostStop] * (1 + 1e-12)) ghostStop = j;
        }
        double scale = 1.0 / ratios[ghostStop];

        SetStop(sys, ghostStop);
        SetAperture(sys, infinite, n[0], scale);
        var p = ParaxialTrace.Trace(sys, n, 0.0);

        int lastLens = lens.Surfaces.Count - 2;
        double toImage = lens.Surfaces[lastLens].Thickness;
        double phi = p.Power * Math.Abs(n[0]);
        double rearFocal = Math.Abs(phi) > 1e-300 ? Math.Abs(n[last - 1]) / phi : double.PositiveInfinity;
        double xp = p.ExitPupilFromLastSurface;

        double t = 1.0;
        for (int j = 1; j < last; j++)
        {
            double r = reflectance[layout.Origin[j]];
            t *= layout.Reflects[j] ? r : 1.0 - r;
        }
        // What enters the ghost's pupil, of what enters the lens's: the pupil's area, or for a
        // finite object the solid angle, which the ghost's stop may have cut down.
        double fraction = Math.Pow(scale / Math.Abs(nominalRay), 2);
        double power = options.InputPower * Math.Min(1.0, fraction) * t;
        double radius = Math.Abs(p.Y[last]);
        double irradiance = radius > 0 ? power / (Math.PI * radius * radius) : double.PositiveInfinity;

        return new Ghost
        {
            Layout = layout,
            Paraxial = p,
            StopRatios = ratios,
            StopLayoutSurface = ghostStop,
            Anomalous = layout.Origin[ghostStop] != stop,
            RearPrincipalPlane = p.Bfl - rearFocal,
            DeltaZ = toImage - p.ParaxialFocusDistance,
            ExitPupilToGhostImage = p.ParaxialFocusDistance - xp,
            ExitPupilToImage = toImage - xp,
            Transmittance = t,
            Power = power,
            Irradiance = irradiance,
        };
    }

    /// <summary>Normal-incidence reflectance between two media, or the coating's for glass in air.</summary>
    public static double Reflectance(double n1, double n2, double? coated = null)
    {
        n1 = Math.Abs(n1);
        n2 = Math.Abs(n2);
        if (Math.Abs(n1 - n2) < 1e-12) return 0.0;
        bool airGlass = Math.Abs(n1 - 1.0) < 1e-3 || Math.Abs(n2 - 1.0) < 1e-3;
        if (coated is double r && airGlass) return r;
        double f = (n1 - n2) / (n1 + n2);
        return f * f;
    }

    private static void SetStop(OpticalSystem sys, int surface)
    {
        foreach (var s in sys.Surfaces) s.IsStop = false;
        sys.Surfaces[surface].IsStop = true;
    }

    /// <summary>
    /// The aperture that makes the marginal ray start at height <paramref name="size"/> at the
    /// first surface (object at infinity) or at slope <paramref name="size"/> (finite object).
    /// </summary>
    private static void SetAperture(OpticalSystem sys, bool infinite, double n0, double size)
    {
        sys.Aperture = infinite
            ? new Aperture(ApertureType.EPD, 2.0 * size)
            : new Aperture(ApertureType.ObjectSpaceNA, Math.Abs(n0) * Math.Sin(Math.Atan(size)));
    }
}
