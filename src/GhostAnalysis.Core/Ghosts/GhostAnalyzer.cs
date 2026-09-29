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

    /// <summary>
    /// Whether each real ray is aimed at the stop - launched where it passes through the stop at the
    /// point the pupil grid names - rather than across the paraxial entrance pupil, which a fast
    /// lens's pupil aberrations make overfill the stop.
    /// </summary>
    public bool AimRays { get; init; } = true;

    /// <summary>The sensor's size and grating. By default it has no edge and does not diffract.</summary>
    public Sensor Sensor { get; init; } = new();

    /// <summary>
    /// The wavelengths, in micrometres, with their weights - the share of the light entering at
    /// each. Null for the lens's own. A single analysis (<see cref="GhostAnalyzer.Analyze"/>) is at
    /// <see cref="Wavelength"/>; a spectral one (<see cref="GhostSpectrum.Analyze"/>) at each of these.
    /// </summary>
    public IReadOnlyList<(double Um, double Weight)>? Wavelengths { get; init; }

    /// <summary>The wavelength a single analysis is at, in micrometres; null for the lens's primary.</summary>
    public double? Wavelength { get; init; }

    /// <summary>These options, at another wavelength.</summary>
    public GhostOptions At(double um) => new()
    {
        Reflections = Reflections, ImageReflects = ImageReflects, ImageReflectance = ImageReflectance,
        CoatedReflectance = CoatedReflectance, InputPower = InputPower, Fields = Fields, FieldExtent = FieldExtent,
        FieldSteps = FieldSteps, RealRays = RealRays, PupilSamples = PupilSamples, AimRays = AimRays, Sensor = Sensor,
        Wavelengths = Wavelengths, Wavelength = um,
    };
}

/// <summary>Every ghost of one lens, analysed at one wavelength.</summary>
public sealed class GhostResult
{
    public required OpticalSystem Lens { get; init; }
    public required GhostOptions Options { get; init; }
    public required double Wavelength { get; init; }

    /// <summary>The lens's own paraxial trace.</summary>
    public required ParaxialResult Nominal { get; init; }

    /// <summary>The lens's own indices after each surface, at <see cref="Wavelength"/>.</summary>
    public required IReadOnlyList<double> NominalIndices { get; init; }

    /// <summary>
    /// Each lens surface's clear semi-diameter as the analysis used it: the file's, or where the
    /// file gives none, the lens's own beam's (see <see cref="ApertureComputed"/>). 0 for the object
    /// and the image.
    /// </summary>
    public required IReadOnlyList<double> Apertures { get; init; }

    /// <summary>Which of <see cref="Apertures"/> were computed from the lens's beam, the file giving none.</summary>
    public required IReadOnlyList<bool> ApertureComputed { get; init; }

    /// <summary>
    /// The sensor as the analysis used it: the one given, or, given no size, bounded by the lens's
    /// image circle.
    /// </summary>
    public required Sensor Sensor { get; init; }

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

    /// <summary>The ghost that reflects from these surfaces - in its zeroth order, if the sensor diffracts.</summary>
    public Ghost? Find(params int[] surfaces) =>
        Ghosts.FirstOrDefault(g => g.Path.Surfaces.SequenceEqual(surfaces) && g.Orders.All(o => o == (0, 0)));

    /// <summary>The ghost that reflects from these surfaces, in these orders at its reflections from the sensor.</summary>
    public Ghost? Find(int[] surfaces, params (int M, int N)[] orders) =>
        Ghosts.FirstOrDefault(g => g.Path.Surfaces.SequenceEqual(surfaces) && g.Orders.SequenceEqual(orders));
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
        IReadOnlyList<double> Fields, double ReferenceField, double ImageAtReference,
        double[] Apertures, Sensor Sensor);

    public static GhostResult Analyze(OpticalSystem lens, GlassCatalog catalog, GhostOptions? options = null)
    {
        if (lens == null) throw new ArgumentNullException(nameof(lens));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        options ??= new GhostOptions();
        if (lens.Wavelengths.Count == 0) throw new InvalidOperationException("The lens has no wavelengths.");
        foreach (double r in new[] { options.ImageReflectance, options.CoatedReflectance ?? 0.0 })
            if (r < 0.0 || r > 1.0)
                throw new ArgumentOutOfRangeException(nameof(options), $"A reflectance is a fraction from 0 to 1, not {r} (10% is 0.1).");

        double wave = options.Wavelength ?? lens.Wavelengths[Math.Max(0, lens.PrimaryWavelengthIndex)].Value;
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

        // Where the glass ends, and where the sensor does. A file that gives no semi-diameter for a
        // surface does not mean the surface is endless: its reflections, and the rays through it,
        // stop where the lens's own beam needs it to stop. A sensor given no size is the image
        // circle: light landing beyond what the lens images is not on the sensor.
        var (apertures, computed) = Apertures(lens, n, nominal, options.AimRays);
        apertures[stop] = stopSd;
        computed[stop] = false;
        var sensor = options.Sensor;
        if (!sensor.Bounded)
        {
            double circle = ImageCircle(lens, n, nominal, options.AimRays);
            if (circle > 0) sensor = sensor.WithImageCircle(circle);
        }

        var context = new Context(lens, options, wave, stop, stopSd, infinite, nominalRay, reflectance,
                                  fields, reference, imageAtReference, apertures, sensor);

        // The layouts and their indices first, in turn - the catalogs are the one shared thing -
        // then each ghost's traces, which touch nothing but its own layout, side by side. A ghost
        // that reflects from a diffracting sensor is one ghost per order there, each with a layout
        // of its own, since the analysis marks its stop on it.
        var work = new List<Work>();
        foreach (var path in GhostPath.Enumerate(image - 1, options.Reflections, options.ImageReflects))
        {
            // A surface with no index step - a dummy, a stop in air - sends nothing back.
            if (path.Surfaces.Any(k => reflectance[k] <= 0.0)) continue;
            var layout = GhostLayout.Build(lens, path);
            var gn = IndexResolver.Build(layout.System, catalog, wave);
            var onSensor = Enumerable.Range(0, layout.Origin.Count).Where(j => layout.Reflects[j] && layout.Origin[j] == image).ToList();
            if (onSensor.Count == 0 || !options.Sensor.Diffracts)
            {
                work.Add(new Work(layout, gn, Array.Empty<(int, int)>(), Array.Empty<(double, double)>(), 1.0));
                continue;
            }
            var orders = options.Sensor.Orders(wave, gn[onSensor[0]]);
            foreach (var combo in Combinations(orders, onSensor.Count))
            {
                double efficiency = combo.Aggregate(1.0, (e, o) => e * o.Efficiency);
                if (efficiency < options.Sensor.MinimumEfficiency) continue;
                var kicks = combo.Select((o, k) => options.Sensor.Kick(o.M, o.N, wave, gn[onSensor[k]])).ToArray();
                work.Add(new Work(GhostLayout.Build(lens, path), gn, combo.Select(o => (o.M, o.N)).ToArray(), kicks, efficiency));
            }
        }
        var ghosts = new Ghost[work.Count];
        Parallel.For(0, work.Count, i => ghosts[i] = Analyze(context, work[i]));

        return new GhostResult
        {
            Lens = lens,
            Options = options,
            Wavelength = wave,
            Nominal = nominal,
            NominalIndices = n,
            Apertures = apertures,
            ApertureComputed = computed,
            Sensor = sensor,
            Reflectance = reflectance,
            Fields = fields,
            Ghosts = ghosts,
            Unresolved = unresolved.Distinct().ToList(),
        };
    }

    /// <summary>One ghost to analyse: its layout, indices, and its orders and their kicks at the sensor.</summary>
    private sealed record Work(GhostLayout Layout, double[] N, (int M, int N)[] Orders,
                               (double L, double M)[] Kicks, double Efficiency);

    /// <summary>Every way of choosing one order at each of <paramref name="count"/> reflections from the sensor.</summary>
    private static IEnumerable<(int M, int N, double Efficiency)[]> Combinations(
        IReadOnlyList<(int M, int N, double Efficiency)> orders, int count)
    {
        if (count == 0) { yield return Array.Empty<(int, int, double)>(); yield break; }
        foreach (var rest in Combinations(orders, count - 1))
            foreach (var o in orders)
                yield return rest.Append(o).ToArray();
    }

    private static Ghost Analyze(Context c, Work w)
    {
        var lens = c.Lens;
        var layout = w.Layout;
        var n = w.N;
        var sys = layout.System;
        int last = sys.Surfaces.Count - 1;
        int image = lens.Surfaces.Count - 1;

        // Each layout surface's semi-diameter, or 0 where it has none. The image's in a lens
        // file is the height of the image, not the size of the sensor, so the sensor,
        // reflecting, is taken to catch the whole beam.
        var sd = new double[sys.Surfaces.Count];
        for (int j = 1; j < last; j++)
            sd[j] = layout.Origin[j] == image ? 0.0 : c.Apertures[layout.Origin[j]];

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

        // Of the sensor's reflected light, the share that goes into these orders.
        double t = w.Efficiency;
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
        // Real rays through this ghost: stopped by its apertures and the sensor's edges, and turned
        // into its orders at the sensor.
        var onSensor = Enumerable.Range(0, sys.Surfaces.Count).Select(j => layout.Reflects[j] && layout.Origin[j] == image).ToArray();
        var tracer = new GhostTracer(sys, n, p, sd, ghostStop, onSensor, c.Sensor, w.Kicks, c.Options.AimRays);

        // Paraxially an order is a kick to the ray's slope at the sensor, which carries on through
        // the rest of the ghost to a fixed displacement on the sensor, whatever the field.
        double shiftX = 0.0, shiftY = 0.0;
        var sensorHits = Enumerable.Range(0, onSensor.Length).Where(j => onSensor[j]).ToList();
        for (int k = 0; k < w.Kicks.Length && k < sensorHits.Count; k++)
        {
            int j = sensorHits[k];
            double carry = Carry(sys, p, j);
            // The paraxial slope is M/N, and N, after the reflection, runs the way the index's sign does.
            shiftX += carry * w.Kicks[k].L * Math.Sign(p.N[j]);
            shiftY += carry * w.Kicks[k].M * Math.Sign(p.N[j]);
        }

        var crossings = new List<(double Field, string Kind)>();
        var extra = new List<(double Field, string? Kind)>();
        double top = c.Fields.Count == 0 ? 0.0 : c.Fields.Max();
        if (c.Options.RealRays && top > 0)
        {
            crossings.AddRange(tracer.Crossings(0.0, top));
            extra.AddRange(crossings.Select(x => (x.Field, (string?)x.Kind)));

            const int scan = 120;
            double best = -1, bestField = double.NaN;
            for (int i = 0; i <= scan; i++)
            {
                double h = top * i / scan;
                double e = Spot(tracer, h, power, airy, 11, finest: 47).Irradiance;
                if (e > best) { best = e; bestField = h; }
            }
            if (best > 0 && !c.Fields.Contains(bestField)) extra.Add((bestField, "P"));
        }

        var fieldList = c.Fields.Select(h => (Field: h, Kind: (string?)null))
                                .Concat(extra)
                                .OrderBy(x => x.Field);
        var fields = new List<GhostField>();
        foreach (var (h, kind) in fieldList)
            fields.Add(AtField(c, layout, tracer, p, off, sd, ghostStop, h, power, radius, airy, deltaZ, sagT, sagS,
                               shiftX, shiftY, kind));

        return new Ghost
        {
            Layout = layout,
            Paraxial = p,
            Tracer = tracer,
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
            Orders = w.Orders,
            OrderEfficiency = w.Efficiency,
            Wavelength = c.Wavelength,
            AiryRadius = airy,
            PupilSamples = c.Options.PupilSamples,
        };
    }

    /// <summary>A ghost's real spot at one field, as its field-by-field analysis finds it: irradiance, centroid, RMS.</summary>
    internal static (double Irradiance, double X, double Y, double Rms) SpotAt(Ghost g, double field)
    {
        var s = Spot(g.Tracer, field, g.Power, g.AiryRadius, g.PupilSamples);
        return (s.Irradiance, s.X, s.Y, s.Rms);
    }

    /// <summary>
    /// Where a paraxial ray leaving layout surface <paramref name="from"/> on axis with unit slope
    /// lands on the sensor: how far a kick to the slope there moves the ghost.
    /// </summary>
    private static double Carry(OpticalSystem sys, ParaxialResult p, int from)
    {
        int last = sys.Surfaces.Count - 1;
        double y = 0.0, u = 1.0;
        for (int i = from + 1; i <= last; i++)
        {
            y += u * sys.Surfaces[i - 1].Thickness;
            if (i == last) break;
            double w = p.N[i - 1] * u - y * (p.N[i] - p.N[i - 1]) * sys.Surfaces[i].VertexCurvature;
            u = w / p.N[i];
        }
        return y;
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
    public static (double Tangential, double Sagittal) RealFoci(OpticalSystem sys, double[] n, ParaxialResult p, double field) =>
        GhostTracer.Plain(sys, n, p).Foci(field);

    /// <summary>
    /// The fields between <paramref name="from"/> and <paramref name="to"/> at which the ghost's real
    /// tangential or sagittal focus is on the sensor (see <see cref="GhostTracer.Crossings"/>).
    /// </summary>
    public static IEnumerable<(double Field, string Kind)> RealCrossings(OpticalSystem sys, double[] n, ParaxialResult p,
                                                                         double from, double to, int steps = 120) =>
        GhostTracer.Plain(sys, n, p).Crossings(from, to, steps);

    /// <summary>The ghost at one field: paraxially, and by real rays when asked.</summary>
    private static GhostField AtField(Context c, GhostLayout layout, GhostTracer tracer, ParaxialResult p,
                                      ParaxialResult? off, double[] sd, int ghostStop, double h,
                                      double power, double radius, double airy,
                                      double deltaZ, double sagT, double sagS,
                                      double shiftX, double shiftY, string? crossing)
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
        double center = (onAxis ? 0.0 : off != null ? off.Ybar[^1] * s : double.NaN) + shiftY;
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
                Field = h, ImageHeight = imageHeight, ParaxialCenter = center, ParaxialCenterX = shiftX, ParaxialRadius = radius,
                ParaxialTransmitted = passed, ParaxialIrradiance = paraxialIrradiance, Traced = false,
                PredictedTangentialFocus = predictedT, PredictedSagittalFocus = predictedS,
            };
        }
        var (focusT, focusS) = tracer.Foci(h);
        var spot = Spot(tracer, h, power, airy, c.Options.PupilSamples);
        var chief = tracer.Trace(h, 0.0, 0.0, clip: true);

        return new GhostField
        {
            Field = h, ImageHeight = imageHeight, ParaxialCenter = center, ParaxialCenterX = shiftX, ParaxialRadius = radius,
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
    private static SpotResult Spot(GhostTracer tracer, double h, double power, double airy, int across, int finest = 161)
    {
        // A beam cut down to a sliver by vignetting reaches the sensor as a handful of rays, whose
        // spread says nothing: the grid is refined until enough arrive to measure it.
        const int enough = 64;
        int m = Math.Max(3, across | 1);           // odd, so the chief ray is on it
        var spot = Sample(tracer, h, power, airy, m, out int arrived);
        while (arrived > 0 && arrived < enough && m < finest)
        {
            m = Math.Min(finest, 2 * m + 1);
            spot = Sample(tracer, h, power, airy, m, out arrived);
        }
        return spot;
    }

    private static SpotResult Sample(GhostTracer tracer, double h, double power, double airy, int m, out int arrived)
    {
        int launched = 0;
        arrived = 0;
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
                var hit = tracer.Trace(h, py, px, clip: true);
                if (hit == null) continue;
                arrived++;
                double x = hit.Value.X, y = hit.Value.Y;
                xs.Add(x); ys.Add(y);
                sx += x; sy += y; sxx += x * x; syy += y * y;
            }
        }

        // Of the paraxial pupil's light, what arrives: aimed, the grid fills the real pupil, whose
        // area is the light the stop lets in.
        double transmitted = launched > 0 ? (double)arrived / launched * tracer.PupilArea(h) : 0.0;
        if (arrived == 0) return new SpotResult(0.0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0.0);
        double cx = sx / arrived, cy = sy / arrived;
        double rms = Math.Sqrt(Math.Max(0.0, (sxx + syy) / arrived - cx * cx - cy * cy));
        double max = 0.0;
        for (int k = 0; k < xs.Count; k++) max = Math.Max(max, Math.Sqrt((xs[k] - cx) * (xs[k] - cx) + (ys[k] - cy) * (ys[k] - cy)));
        // Diffraction by the part of the pupil that gets through: a beam vignetted to a fraction of
        // its area spreads as an aperture of that size does, wider by the square root.
        double effective = Math.Max(Math.Sqrt(2.0) * rms, airy / Math.Sqrt(transmitted));
        double lit = effective > 0 ? power * transmitted / (Math.PI * effective * effective) : double.PositiveInfinity;
        return new SpotResult(transmitted, cx, cy, rms, max, effective, lit);
    }

    /// <summary>
    /// Each surface's clear semi-diameter: the file's where it gives one; otherwise the aperture
    /// the lens needs to pass its own full beam at every field up to the largest it specifies -
    /// the largest height any ray of that beam reaches there, around the whole rim of the pupil -
    /// which is how a design program sizes a surface it is not told the size of. Nothing inside
    /// the lens's field is vignetted by these; beyond it, and for ghost light that strays outside
    /// the lens's own beam, they are the edges of the glass. 0 where neither says: the object, the
    /// image, a surface no ray reaches.
    /// </summary>
    public static (double[] Apertures, bool[] Computed) Apertures(OpticalSystem lens, double[] n, ParaxialResult nominal,
                                                                  bool aim = true)
    {
        int count = lens.Surfaces.Count, last = count - 2;
        var aperture = new double[count];
        var computed = new bool[count];
        var envelope = new double[count];

        // The lens's fields and the steps between them, its largest always among them; and the
        // pupil's rim, finely enough that no rim ray between two samples reaches further than they do
        // by more than a part in ten thousand.
        double top = lens.Fields.Count == 0 ? 0.0 : lens.Fields.Max(f => Math.Abs(f.Y));
        var fields = top > 0
            ? Enumerable.Range(0, 11).Select(i => top * i / 10.0).Concat(lens.Fields.Select(f => Math.Abs(f.Y))).Distinct().ToList()
            : new List<double> { 0.0 };
        const int rim = 64;
        var pupil = new List<(double Py, double Px)>();
        for (int k = 0; k < rim; k++) pupil.Add((Math.Cos(2 * Math.PI * k / rim), Math.Sin(2 * Math.PI * k / rim)));
        pupil.Add((0.0, 0.0));
        var tracer = LensTracer(lens, n, nominal, aim);
        foreach (double h in fields)
            foreach (var (py, px) in pupil)
            {
                try
                {
                    tracer.Trace(h, py, px, clip: false, (i, hit) =>
                    {
                        if (i <= last) envelope[i] = Math.Max(envelope[i], Math.Sqrt(hit.X * hit.X + hit.Y * hit.Y));
                    });
                }
                catch (InvalidOperationException) { }
            }

        // A rim ray between two samples reaches at most 1 - cos(π / rim) further than they do: the
        // aperture is opened by that, so no ray of the beam is clipped, and by no more.
        double between = 1.0 + (1.0 - Math.Cos(Math.PI / rim));
        for (int i = 1; i <= last; i++)
        {
            if (lens.Surfaces[i].SemiDiameter > 0) aperture[i] = lens.Surfaces[i].SemiDiameter;
            else if (envelope[i] > 0) { aperture[i] = envelope[i] * between; computed[i] = true; }
        }
        return (aperture, computed);
    }

    /// <summary>The image circle: where the lens's real chief ray of its largest field lands. 0 for a lens with no field.</summary>
    public static double ImageCircle(OpticalSystem lens, double[] n, ParaxialResult nominal, bool aim = true)
    {
        double top = lens.Fields.Count == 0 ? 0.0 : lens.Fields.Max(f => Math.Abs(f.Y));
        if (top <= 0) return 0.0;
        try
        {
            var end = LensTracer(lens, n, nominal, aim).Trace(top, 0.0, 0.0, clip: false);
            return end is { } e ? Math.Sqrt(e.X * e.X + e.Y * e.Y) : 0.0;
        }
        catch (InvalidOperationException) { return 0.0; }
    }

    /// <summary>The lens's own rays, unclipped: aimed at its stop, or across its paraxial pupil.</summary>
    private static GhostTracer LensTracer(OpticalSystem lens, double[] n, ParaxialResult nominal, bool aim)
    {
        int count = lens.Surfaces.Count;
        int stop = lens.StopSurfaceIndex;
        if (stop < 1 || stop >= count - 1) stop = count - 2;
        return new GhostTracer(lens, n, nominal, new double[count], stop, new bool[count], null, null, aim);
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
