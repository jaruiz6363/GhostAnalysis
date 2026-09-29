using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>What to count as a ghost, how much light the surfaces send back, and where to look.</summary>
public sealed class GhostOptions
{
    /// <summary>How many reflections a ghost has: 2, 4, ... Each pair more is weaker by about R^2.</summary>
    public int Reflections { get; init; } = 2;

    /// <summary>Whether the image - the sensor - reflects. It does; the papers leave it out.</summary>
    public bool ImageReflects { get; init; } = true;

    /// <summary>
    /// The sensor's reflectance, as a fraction. A lens file does not say, and a sensor is not
    /// glass, so Fresnel cannot: it is a property of the sensor and its cover, to be measured
    /// or looked up. The default is a placeholder of a few per cent.
    /// </summary>
    public double ImageReflectance { get; init; } = 0.05;

    /// <summary>
    /// The reflectance of every glass-air surface, as a fraction, for a coated lens; null for
    /// uncoated glass, whose reflectance is Fresnel's at normal incidence. A cemented surface is
    /// always Fresnel's: the index step there is small, and so is what it sends back.
    /// </summary>
    public double? CoatedReflectance { get; init; }

    /// <summary>Power entering the lens's entrance pupil, in whatever unit the irradiances should come out in per lens unit squared.</summary>
    public double InputPower { get; init; } = 1.0;

    /// <summary>
    /// The fields to analyse, in the lens's field units. Null for a sweep from the axis to
    /// <see cref="FieldExtent"/> times the lens's largest field, in <see cref="FieldSteps"/> steps:
    /// a bright source just outside the picture still sends its ghosts into it.
    /// </summary>
    public IReadOnlyList<double>? Fields { get; init; }

    public double FieldExtent { get; init; } = 1.2;
    public int FieldSteps { get; init; } = 12;

    /// <summary>Whether to trace real rays for each ghost's spot; false for the paraxial analysis only.</summary>
    public bool RealRays { get; init; } = true;

    /// <summary>Rays across the pupil's diameter; the grid inside the pupil is traced.</summary>
    public int PupilSamples { get; init; } = 21;
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

    /// <summary>The fields analysed, in the lens's field units.</summary>
    public required IReadOnlyList<double> Fields { get; init; }

    /// <summary>The ghosts, in the order <see cref="GhostPath.Enumerate"/> gives them.</summary>
    public required IReadOnlyList<Ghost> Ghosts { get; init; }

    /// <summary>Glasses the catalogs did not have, traced as air. Empty for a usable result.</summary>
    public required IReadOnlyList<string> Unresolved { get; init; }

    /// <summary>The ghosts, brightest at their worst field first.</summary>
    public IEnumerable<Ghost> Ranked => Ghosts.OrderByDescending(g => g.PeakIrradiance);

    public Ghost? Find(params int[] surfaces) => Ghosts.FirstOrDefault(g => g.Path.Surfaces.SequenceEqual(surfaces));
}

/// <summary>
/// Paraxial ghost image analysis: Abd El-Maksoud and Sasian, "Paraxial ghost image analysis",
/// Proc. SPIE 7428, 742807 (2009), and "Modeling and analyzing ghost images for incoherent
/// optical systems", Appl. Opt. 50, 2305 (2011) - with real rays traced through each ghost.
///
/// <para>Each ghost is unfolded (<see cref="GhostLayout"/>) and traced as a lens of its own.
/// Its aperture stop is found the 2009 paper's way (section 6): an axial ray is traced through
/// the layout, and the surface it fills most - height over semi-diameter - is the stop, which
/// need not be the lens's. The ray scaled to fill that surface exactly is the ghost's marginal
/// ray, and its pupils, focal length, image and size at the lens's image follow from the
/// ordinary paraxial trace.</para>
///
/// <para>Off axis, the ghost's chief ray - through the centre of its own stop - puts the centre
/// of its disc on the sensor (2009 eq. 11, 2011 eq. 17), and the surface that first cuts its
/// beam is its field stop (2009 eq. 10). Then real rays: a grid across the ghost's entrance
/// pupil, traced through the unfolded ghost to the sensor, each stopped where it falls outside
/// a surface's semi-diameter. Where the ghost really lands, how large it really is and how much
/// of it gets through come from those - which is where a ghost that is defocused on axis turns
/// out to focus on the sensor off it.</para>
///
/// <para>Its light is the 2011 paper's (section 9): what enters its entrance pupil, times a
/// Fresnel transmittance at every surface crossed and a reflectance at every reflection, over
/// the area it covers on the sensor.</para>
/// </summary>
public static class GhostAnalyzer
{
    /// <summary>What every ghost of one lens shares.</summary>
    private sealed record Context(
        OpticalSystem Lens, GhostOptions Options, double Wavelength,
        int Stop, double StopSd, bool Infinite, double NominalRay, double[] Reflectance,
        IReadOnlyList<double> Fields, double ReferenceField, double ImageAtReference);

    public static GhostResult Analyze(OpticalSystem lens, GlassCatalog catalog, GhostOptions? options = null)
    {
        if (lens == null) throw new ArgumentNullException(nameof(lens));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        options ??= new GhostOptions();
        if (lens.Wavelengths.Count == 0) throw new InvalidOperationException("The lens has no wavelengths.");
        foreach (double r in new[] { options.ImageReflectance, options.CoatedReflectance ?? 0.0 })
            if (r < 0.0 || r > 1.0)
                throw new ArgumentOutOfRangeException(nameof(options), $"A reflectance is a fraction from 0 to 1, not {r} (10% is 0.1).");

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

        // The fields, and the lens's own image of the largest: every paraxial chief ray is
        // linear in the field, so one trace at the largest serves them all.
        var fields = FieldsFor(lens, options);
        double reference = fields.Count == 0 ? 0.0 : fields.Max(Math.Abs);
        double imageAtReference = double.NaN;
        if (reference > 0)
        {
            try { imageAtReference = ParaxialTrace.Trace(lens, n, reference).Ybar[^1]; }
            catch (InvalidOperationException) { }
        }

        var context = new Context(lens, options, wave, stop, stopSd, infinite, nominalRay, reflectance,
                                  fields, reference, imageAtReference);

        // The layouts and their indices first, in turn - the catalogs are the one shared thing -
        // then each ghost's traces, which touch nothing but its own layout, side by side.
        var work = new List<(GhostLayout Layout, double[] N)>();
        foreach (var path in GhostPath.Enumerate(image - 1, options.Reflections, options.ImageReflects))
        {
            // A surface with no index step - a dummy, a stop in air - sends nothing back.
            if (path.Surfaces.Any(k => reflectance[k] <= 0.0)) continue;
            var layout = GhostLayout.Build(lens, path);
            work.Add((layout, IndexResolver.Build(layout.System, catalog, wave)));
        }
        var ghosts = new Ghost[work.Count];
        Parallel.For(0, work.Count, i => ghosts[i] = Analyze(context, work[i].Layout, work[i].N));

        return new GhostResult
        {
            Lens = lens,
            Options = options,
            Wavelength = wave,
            Nominal = nominal,
            Reflectance = reflectance,
            Fields = fields,
            Ghosts = ghosts,
            Unresolved = unresolved.Distinct().ToList(),
        };
    }

    private static Ghost Analyze(Context c, GhostLayout layout, double[] n)
    {
        var lens = c.Lens;
        var sys = layout.System;
        int last = sys.Surfaces.Count - 1;
        int image = lens.Surfaces.Count - 1;

        // Each layout surface's semi-diameter, or 0 where it has none. The image's in a lens
        // file is the height of the image, not the size of the sensor, so the sensor,
        // reflecting, is taken to catch the whole beam.
        var sd = new double[sys.Surfaces.Count];
        for (int j = 1; j < last; j++)
            sd[j] = layout.Origin[j] == c.Stop ? c.StopSd
                  : layout.Origin[j] == image ? 0.0
                  : Math.Max(0.0, sys.Surfaces[j].SemiDiameter);

        // The potential marginal ray: from the axial object point, unit height at surface 1
        // (unit slope from a finite object). Where the stop is marked does not change it.
        int firstStop = Enumerable.Range(1, last - 1).First(j => layout.Origin[j] == c.Stop);
        SetStop(sys, firstStop);
        SetAperture(sys, c.Infinite, n[0], 1.0);
        var unit = ParaxialTrace.Trace(sys, n, 0.0);

        var ratios = new double[sys.Surfaces.Count];
        ratios[0] = ratios[last] = double.NaN;
        int ghostStop = firstStop;
        for (int j = 1; j < last; j++)
        {
            ratios[j] = sd[j] > 0 ? Math.Abs(unit.Y[j]) / sd[j] : double.NaN;
            if (!double.IsNaN(ratios[j]) && ratios[j] > ratios[ghostStop] * (1 + 1e-12)) ghostStop = j;
        }
        double scale = 1.0 / ratios[ghostStop];

        SetStop(sys, ghostStop);
        SetAperture(sys, c.Infinite, n[0], scale);
        var p = ParaxialTrace.Trace(sys, n, 0.0);
        ParaxialResult? off = null;
        if (c.ReferenceField > 0)
        {
            // Refused when the ghost's stop is imaged at infinity in object space and the field
            // is an angle: no chief ray leaves at an angle then. The ghost is analysed on axis.
            try { off = ParaxialTrace.Trace(sys, n, c.ReferenceField); }
            catch (InvalidOperationException) { }
        }

        int lastLens = lens.Surfaces.Count - 2;
        double toImage = lens.Surfaces[lastLens].Thickness;
        double phi = p.Power * Math.Abs(n[0]);
        double rearFocal = Math.Abs(phi) > 1e-300 ? Math.Abs(n[last - 1]) / phi : double.PositiveInfinity;
        double xp = p.ExitPupilFromLastSurface;

        double t = 1.0;
        for (int j = 1; j < last; j++)
        {
            double r = c.Reflectance[layout.Origin[j]];
            t *= layout.Reflects[j] ? r : 1.0 - r;
        }
        // What enters the ghost's pupil, of what enters the lens's: the pupil's area, or for a
        // finite object the solid angle, which the ghost's stop may have cut down.
        double fraction = Math.Pow(scale / Math.Abs(c.NominalRay), 2);
        double power = c.Options.InputPower * Math.Min(1.0, fraction) * t;
        double radius = Math.Abs(p.Y[last]);
        double irradiance = radius > 0 ? power / (Math.PI * radius * radius) : double.PositiveInfinity;

        // The field stop: the surface whose aperture the chief ray, with the beam about it, reaches
        // first as the field grows (2009 eq. 10).
        int fieldStop = -1;
        double unvignetted = off == null ? double.NaN : double.PositiveInfinity;
        if (off != null)
        {
            for (int j = 1; j < last; j++)
            {
                double chief = Math.Abs(off.Ybar[j]);
                if (sd[j] <= 0 || j == ghostStop || chief < 1e-15) continue;
                double limit = Math.Max(0.0, sd[j] - Math.Abs(p.Y[j])) / chief;   // as a fraction of the reference
                limit = c.Lens.FieldType == FieldType.ObjectAngle
                    ? Math.Atan(limit * Math.Tan(c.ReferenceField * Math.PI / 180.0)) * 180.0 / Math.PI
                    : limit * c.ReferenceField;
                if (limit < unvignetted) { unvignetted = limit; fieldStop = j; }
            }
        }

        double magnification = off != null && Math.Abs(c.ImageAtReference) > 1e-300
            ? off.Ybar[^1] / c.ImageAtReference
            : double.NaN;

        // The ghost's cone at the sensor sets the smallest spot it can make: the Airy radius.
        double slope = Math.Abs(p.U[last - 1]);
        double airy = slope > 1e-15 ? 1.22 * c.Wavelength * 1e-3 / (2.0 * slope) : 0.0;

        // Its image surfaces, by its own third-order sums: how far its tangential and sagittal
        // foci stand short of its paraxial image at the reference field. They grow as the field
        // squared, so a ghost focused short of the sensor on axis (ΔZ > 0) comes into focus on it
        // off axis wherever its image surface curves back to meet it (2011, eqs. 23-25).
        double deltaZ = toImage - p.ParaxialFocusDistance;
        double sagT = double.NaN, sagS = double.NaN;
        if (off != null)
        {
            var seidel = SeidelCoefficients.Compute(sys, n, n, n, off);
            var surfaces = FieldSurfaces.Compute(sys, seidel, off, c.ReferenceField);
            sagT = surfaces.TangentialSag;
            sagS = surfaces.SagittalSag;
        }
        double predictedT = Crossing(c, deltaZ, sagT), predictedS = Crossing(c, deltaZ, sagS);

        // The real crossings: where the real foci either side of the chief ray reach the sensor.
        // And, because those foci speak only for the middle of the beam - a fast, aberrated ghost
        // can have its chief ray's focus on the sensor and its spot no smaller for it - a fine scan
        // of the real spot itself over field, for where the ghost is really brightest.
        var crossings = new List<(double Field, string Kind)>();
        var extra = new List<(double Field, string? Kind)>();
        double top = c.Fields.Count == 0 ? 0.0 : c.Fields.Max();
        if (c.Options.RealRays && top > 0)
        {
            crossings.AddRange(RealCrossings(sys, n, p, 0.0, top));
            extra.AddRange(crossings.Select(x => (x.Field, (string?)x.Kind)));

            const int scan = 120;
            double best = -1, bestField = double.NaN;
            for (int i = 0; i <= scan; i++)
            {
                double h = top * i / scan;
                double e = Spot(sys, n, p, sd, ghostStop, h, power, airy, 11).Irradiance;
                if (e > best) { best = e; bestField = h; }
            }
            if (best > 0 && !c.Fields.Contains(bestField)) extra.Add((bestField, "P"));
        }

        var fieldList = c.Fields.Select(h => (Field: h, Kind: (string?)null))
                                .Concat(extra)
                                .OrderBy(x => x.Field);
        var fields = new List<GhostField>();
        foreach (var (h, kind) in fieldList)
            fields.Add(AtField(c, layout, n, p, off, sd, ghostStop, h, power, radius, airy, deltaZ, sagT, sagS, kind));

        return new Ghost
        {
            Layout = layout,
            Paraxial = p,
            StopRatios = ratios,
            StopLayoutSurface = ghostStop,
            Anomalous = layout.Origin[ghostStop] != c.Stop,
            RearPrincipalPlane = p.Bfl - rearFocal,
            DeltaZ = toImage - p.ParaxialFocusDistance,
            ExitPupilToGhostImage = p.ParaxialFocusDistance - xp,
            ExitPupilToImage = toImage - xp,
            Transmittance = t,
            Power = power,
            Irradiance = irradiance,
            Magnification = magnification,
            FieldStopLayoutSurface = fieldStop,
            UnvignettedField = unvignetted,
            PredictedTangentialCrossing = predictedT,
            PredictedSagittalCrossing = predictedS,
            Crossings = crossings,
            Fields = fields,
        };
    }

    /// <summary>
    /// The field at which a focal surface standing <paramref name="sag"/> short of the ghost's
    /// paraxial image at the reference field meets the sensor, <paramref name="deltaZ"/> beyond
    /// that image: ΔZ + sag s^2 = 0, s the field as a fraction of the reference. NaN if never.
    /// </summary>
    private static double Crossing(Context c, double deltaZ, double sag)
    {
        if (double.IsNaN(sag) || Math.Abs(sag) < 1e-300) return double.NaN;
        double s2 = -deltaZ / sag;
        return s2 > 0 ? FieldFromRatio(c, Math.Sqrt(s2)) : double.NaN;
    }

    /// <summary>The chief ray's starting slope (angle fields: its tangent) or height, as a fraction of the reference field's.</summary>
    private static double RatioFromField(Context c, double h) =>
        c.ReferenceField <= 0 ? 0.0
        : c.Lens.FieldType == FieldType.ObjectAngle
            ? Math.Tan(h * Math.PI / 180.0) / Math.Tan(c.ReferenceField * Math.PI / 180.0)
            : h / c.ReferenceField;

    private static double FieldFromRatio(Context c, double s) =>
        c.Lens.FieldType == FieldType.ObjectAngle
            ? Math.Atan(s * Math.Tan(c.ReferenceField * Math.PI / 180.0)) * 180.0 / Math.PI
            : s * c.ReferenceField;

    /// <summary>
    /// Where the ghost's real light comes to a focus at one field, from the sensor, positive when
    /// short of it: the crossing of two real rays either side of the chief ray - in the field's
    /// plane for the tangential focus, across it for the sagittal - Coddington's foci, traced.
    /// NaN where the rays do not arrive or run parallel.
    /// </summary>
    public static (double Tangential, double Sagittal) RealFoci(OpticalSystem sys, double[] n, ParaxialResult p, double field)
    {
        const double d = 1e-3;
        var up = RealRayTrace.TraceRecord(sys, n, p, field, d, 0.0, atParaxialFocus: false)[^1];
        var down = RealRayTrace.TraceRecord(sys, n, p, field, -d, 0.0, atParaxialFocus: false)[^1];
        var right = RealRayTrace.TraceRecord(sys, n, p, field, 0.0, d, atParaxialFocus: false)[^1];
        var left = RealRayTrace.TraceRecord(sys, n, p, field, 0.0, -d, atParaxialFocus: false)[^1];

        // Two rays at heights y1, y2 on the sensor with slopes t1, t2 meet at z = -(y1 - y2)/(t1 - t2)
        // from it; short of it is -z.
        static double Meet(double a, double b, double ta, double tb) =>
            Math.Abs(ta - tb) > 1e-15 ? (a - b) / (ta - tb) : double.PositiveInfinity;

        double t = up.Ok && down.Ok ? Meet(up.Y, down.Y, up.M / up.N, down.M / down.N) : double.NaN;
        double s = right.Ok && left.Ok ? Meet(right.X, left.X, right.L / right.N, left.L / left.N) : double.NaN;
        return (t, s);
    }

    /// <summary>
    /// The fields between <paramref name="from"/> and <paramref name="to"/> at which the ghost's real
    /// tangential or sagittal focus is on the sensor: a fine scan for a change of sign, each then
    /// closed by bisection. Found by real rays, so a crossing the third order misses is found too.
    /// </summary>
    public static IEnumerable<(double Field, string Kind)> RealCrossings(OpticalSystem sys, double[] n, ParaxialResult p,
                                                                         double from, double to, int steps = 120)
    {
        var found = new List<(double, string)>();
        var foci = new (double T, double S)[steps + 1];
        var h = new double[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            h[i] = from + (to - from) * i / steps;
            foci[i] = RealFoci(sys, n, p, h[i]);
        }
        foreach (var kind in new[] { "T", "S" })
        {
            Func<(double T, double S), double> pick = kind == "T" ? f => f.T : f => f.S;
            for (int i = 0; i < steps; i++)
            {
                double a = pick(foci[i]), b = pick(foci[i + 1]);
                if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Sign(a) == Math.Sign(b) || a == 0) continue;
                double lo = h[i], hi = h[i + 1], flo = a;
                bool ok = true;
                for (int k = 0; k < 60; k++)
                {
                    double mid = 0.5 * (lo + hi);
                    double fm = pick(RealFoci(sys, n, p, mid));
                    if (!double.IsFinite(fm)) { ok = false; break; }
                    if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid;
                }
                // A focus also changes sign by passing through infinity, where the ghost's rays
                // leave parallel; that is no crossing, and there the focus distance grows instead
                // of vanishing.
                double at = 0.5 * (lo + hi);
                double end = pick(RealFoci(sys, n, p, at));
                if (ok && double.IsFinite(end) && Math.Abs(end) < 1e-6 * (Math.Abs(a) + Math.Abs(b)))
                    found.Add((at, kind));
            }
        }
        return found.OrderBy(x => x.Item1);
    }

    /// <summary>The ghost at one field: paraxially, and by real rays when asked.</summary>
    private static GhostField AtField(Context c, GhostLayout layout, double[] n, ParaxialResult p,
                                      ParaxialResult? off, double[] sd, int ghostStop, double h,
                                      double power, double radius, double airy,
                                      double deltaZ, double sagT, double sagS, string? crossing)
    {
        var sys = layout.System;
        int last = sys.Surfaces.Count - 1;
        // Linear in the chief ray's starting slope or height: the tangent of an angle field.
        double s = RatioFromField(c, h);
        bool onAxis = Math.Abs(h) < 1e-15;
        double predictedT = deltaZ + (onAxis ? 0.0 : sagT * s * s);
        double predictedS = deltaZ + (onAxis ? 0.0 : sagS * s * s);

        // Paraxially the beam at each surface is a disc of the marginal ray's radius about the
        // chief ray; what fraction of it the surface's aperture passes, at the worst surface.
        double center = onAxis ? 0.0 : off != null ? off.Ybar[^1] * s : double.NaN;
        double imageHeight = onAxis ? 0.0 : double.IsNaN(c.ImageAtReference) ? double.NaN : c.ImageAtReference * s;
        double passed = onAxis || off != null ? 1.0 : double.NaN;
        if (off != null && !onAxis)
        {
            for (int j = 1; j < last; j++)
            {
                if (sd[j] <= 0 || j == ghostStop) continue;
                double a = Math.Abs(p.Y[j]), d = Math.Abs(off.Ybar[j] * s);
                double share = a < 1e-15 ? (d <= sd[j] ? 1.0 : 0.0)
                             : CircleOverlap(a, sd[j], d) / (Math.PI * a * a);
                passed = Math.Min(passed, share);
            }
        }
        double paraxialIrradiance = radius > 0 ? power * passed / (Math.PI * radius * radius) : double.PositiveInfinity;

        if (!c.Options.RealRays)
        {
            return new GhostField
            {
                Field = h, ImageHeight = imageHeight, ParaxialCenter = center, ParaxialRadius = radius,
                ParaxialTransmitted = passed, ParaxialIrradiance = paraxialIrradiance, Traced = false,
                PredictedTangentialFocus = predictedT, PredictedSagittalFocus = predictedS,
            };
        }
        var (focusT, focusS) = RealFoci(sys, n, p, h);
        var spot = Spot(sys, n, p, sd, ghostStop, h, power, airy, c.Options.PupilSamples);
        var chief = Arrive(sys, n, p, sd, ghostStop, h, 0.0, 0.0);

        return new GhostField
        {
            Field = h, ImageHeight = imageHeight, ParaxialCenter = center, ParaxialRadius = radius,
            ParaxialTransmitted = passed, ParaxialIrradiance = paraxialIrradiance, Traced = true,
            Transmitted = spot.Transmitted, CentroidX = spot.X, CentroidY = spot.Y,
            ChiefY = chief?.Y ?? double.NaN, RmsRadius = spot.Rms, MaxRadius = spot.Max,
            EffectiveRadius = spot.Effective, Irradiance = spot.Irradiance,
            PredictedTangentialFocus = predictedT, PredictedSagittalFocus = predictedS,
            TangentialFocus = focusT, SagittalFocus = focusS, Marker = crossing,
        };
    }

    private readonly record struct SpotResult(double Transmitted, double X, double Y, double Rms, double Max,
                                              double Effective, double Irradiance);

    /// <summary>
    /// The ghost's real spot at one field: a square grid of <paramref name="across"/> rays across
    /// its entrance pupil, those inside the pupil traced. Its irradiance is the power that
    /// arrives over a disc of √2 × its RMS radius, no smaller than the Airy disc.
    /// </summary>
    private static SpotResult Spot(OpticalSystem sys, double[] n, ParaxialResult p, double[] sd, int ghostStop,
                                   double h, double power, double airy, int across)
    {
        int m = Math.Max(3, across | 1);           // odd, so the chief ray is on it
        int launched = 0, arrived = 0;
        double sx = 0, sy = 0, sxx = 0, syy = 0;
        var xs = new List<double>();
        var ys = new List<double>();
        for (int iy = 0; iy < m; iy++)
        {
            double py = -1.0 + 2.0 * iy / (m - 1);
            for (int ix = 0; ix < m; ix++)
            {
                double px = -1.0 + 2.0 * ix / (m - 1);
                if (px * px + py * py > 1.0 + 1e-12) continue;
                launched++;
                var hit = Arrive(sys, n, p, sd, ghostStop, h, py, px);
                if (hit == null) continue;
                arrived++;
                var (x, y) = hit.Value;
                xs.Add(x); ys.Add(y);
                sx += x; sy += y; sxx += x * x; syy += y * y;
            }
        }

        double transmitted = launched > 0 ? (double)arrived / launched : 0.0;
        if (arrived == 0) return new SpotResult(0.0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0.0);
        double cx = sx / arrived, cy = sy / arrived;
        double rms = Math.Sqrt(Math.Max(0.0, (sxx + syy) / arrived - cx * cx - cy * cy));
        double max = 0.0;
        for (int k = 0; k < xs.Count; k++) max = Math.Max(max, Math.Sqrt((xs[k] - cx) * (xs[k] - cx) + (ys[k] - cy) * (ys[k] - cy)));
        double effective = Math.Max(Math.Sqrt(2.0) * rms, airy);
        double lit = effective > 0 ? power * transmitted / (Math.PI * effective * effective) : double.PositiveInfinity;
        return new SpotResult(transmitted, cx, cy, rms, max, effective, lit);
    }

    /// <summary>
    /// Where one real ray lands on the sensor, or null if it does not get there: missed a
    /// surface, was totally internally reflected, or fell outside a surface's semi-diameter.
    /// The ghost's own stop is not checked - the pupil grid is what fills it.
    /// </summary>
    private static (double X, double Y)? Arrive(OpticalSystem sys, double[] n, ParaxialResult p, double[] sd,
                                                int ghostStop, double field, double py, double px)
    {
        var hits = RealRayTrace.TraceRecord(sys, n, p, field, py, px, atParaxialFocus: false);
        int last = hits.Length - 1;
        for (int j = 1; j < last; j++)
        {
            if (!hits[j].Ok) return null;
            if (j == ghostStop || sd[j] <= 0) continue;
            if (Math.Sqrt(hits[j].X * hits[j].X + hits[j].Y * hits[j].Y) > sd[j] * (1 + 1e-9)) return null;
        }
        if (!hits[last].Ok) return null;
        return (hits[last].X, hits[last].Y);
    }

    /// <summary>The fields to analyse: those asked for, or a sweep from the axis past the lens's largest.</summary>
    public static IReadOnlyList<double> FieldsFor(OpticalSystem lens, GhostOptions options)
    {
        if (options.Fields != null) return options.Fields.ToList();
        double max = lens.Fields.Count == 0 ? 0.0 : lens.Fields.Max(f => Math.Abs(f.Y));
        if (max <= 0 || options.FieldSteps < 1) return new[] { 0.0 };
        var list = new List<double>();
        for (int i = 0; i <= options.FieldSteps; i++) list.Add(max * options.FieldExtent * i / options.FieldSteps);
        return list;
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

    /// <summary>Area common to two discs of radii <paramref name="r1"/> and <paramref name="r2"/> whose centres are <paramref name="d"/> apart.</summary>
    public static double CircleOverlap(double r1, double r2, double d)
    {
        if (d >= r1 + r2) return 0.0;
        if (d <= Math.Abs(r1 - r2)) return Math.PI * Math.Pow(Math.Min(r1, r2), 2);
        double a = r1 * r1 * Math.Acos(Math.Clamp((d * d + r1 * r1 - r2 * r2) / (2 * d * r1), -1.0, 1.0));
        double b = r2 * r2 * Math.Acos(Math.Clamp((d * d + r2 * r2 - r1 * r1) / (2 * d * r2), -1.0, 1.0));
        double k = 0.5 * Math.Sqrt(Math.Max(0.0, (-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
        return a + b - k;
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
