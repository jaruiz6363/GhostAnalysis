using System.Globalization;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// The sensor: its size, and the grating its pixels make.
///
/// <para>A sensor is a rectangle centred on the axis; light landing outside it is not seen, and
/// a ghost that reflects from the sensor reflects only from the part of the image plane the
/// sensor covers. Its width runs across the field's plane (x), its height along it (y) - the
/// fields are swept in y.</para>
///
/// <para>It is also a reflective two-dimensional grating. The pixels repeat with a period Λ -
/// for a colour sensor the colour-filter repeat, twice the pixel pitch of a Bayer mosaic - so
/// light it reflects leaves not in one direction but in orders (m, n), each turned from the
/// mirror direction by the grating equation, in direction cosines:</para>
/// <code>
///   L' = L + m λ / (n' Λx)        M' = M + n λ / (n' Λy)
/// </code>
/// <para>and each order is a copy of the ghost, displaced on the sensor. How the reflected light
/// divides among them depends on the pixel's structure - microlens, colour filter, metal - which
/// no one publishes; the default takes each pixel as a reflecting aperture filling a fraction f
/// of the period, whose far field weights order (m, n) by sinc²(m f) sinc²(n f), normalised over
/// the orders that propagate. f = 1 is a plain mirror, all its light in the zeroth order. A
/// measured table can be given instead.</para>
/// </summary>
public sealed class Sensor
{
    /// <summary>Full width across the field's plane (x), in lens units; null for no edge.</summary>
    public double? Width { get; init; }

    /// <summary>Full height along the field's plane (y), in lens units; null for no edge.</summary>
    public double? Height { get; init; }

    /// <summary>Grating period across (x), in micrometres; null for a sensor that does not diffract.</summary>
    public double? PeriodX { get; init; }

    /// <summary>Grating period along (y), in micrometres; the same as across when not given.</summary>
    public double? PeriodY { get; init; }

    /// <summary>The fraction of the period each pixel's reflecting aperture fills, for the default efficiencies.</summary>
    public double FillFactor { get; init; } = 0.5;

    /// <summary>
    /// Measured order efficiencies, (m, n) to the fraction of the sensor's reflected light in
    /// that order; when given, these replace the fill-factor model, and an order not in it gets none.
    /// </summary>
    public IReadOnlyDictionary<(int M, int N), double>? Efficiencies { get; init; }

    /// <summary>The largest |m| and |n| analysed.</summary>
    public int MaxOrder { get; init; } = 2;

    /// <summary>Orders, and combinations of them, carrying less than this fraction of the reflected light are left out.</summary>
    public double MinimumEfficiency { get; init; } = 1e-3;

    /// <summary>
    /// The direction the fields are swept in on the sensor, as an angle in degrees from its height
    /// towards its width: 0 along the height, 90 along the width. Null for the diagonal, to the
    /// sensor's corner (45 degrees for a sensor without a size).
    ///
    /// <para>The lens is rotationally symmetric, so the direction changes nothing in it: the
    /// field's plane is always traced as the meridian (y). What it changes is how that plane lies
    /// on the sensor - where the sensor's edges cut it, and which way the grating's orders turn
    /// the light - and both are worked by rotating between the two frames.</para>
    /// </summary>
    public double? FieldAngle { get; init; } = 0.0;

    /// <summary>The field direction, as an angle in radians from the sensor's height towards its width.</summary>
    private double Angle =>
        FieldAngle is double a ? a * Math.PI / 180.0
        : Width is double w && Height is double h ? Math.Atan2(w, h) : Math.PI / 4;

    /// <summary>
    /// The field's plane in the sensor's frame: <c>D</c> the direction the fields run (the traced
    /// y), <c>E</c> the one across it (the traced x), each as (across the width, along the height).
    /// </summary>
    private ((double X, double Y) D, (double X, double Y) E) Frame
    {
        get
        {
            double a = Angle;
            return ((Math.Sin(a), Math.Cos(a)), (Math.Cos(a), -Math.Sin(a)));
        }
    }

    /// <summary>A point of the traced frame - x across the field's plane, y along it - in the sensor's: across its width, along its height.</summary>
    public (double Across, double Along) ToSensor(double x, double y)
    {
        var (d, e) = Frame;
        return (x * e.X + y * d.X, x * e.Y + y * d.Y);
    }

    /// <summary>How far the sensor reaches from its centre along the field's direction; infinite without a size.</summary>
    public double HalfExtentAlongField
    {
        get
        {
            var (d, _) = Frame;
            double t = double.PositiveInfinity;
            if (Width is double w && Math.Abs(d.X) > 1e-12) t = Math.Min(t, 0.5 * w / Math.Abs(d.X));
            if (Height is double h && Math.Abs(d.Y) > 1e-12) t = Math.Min(t, 0.5 * h / Math.Abs(d.Y));
            return t;
        }
    }

    /// <summary>The field direction in words, for a report.</summary>
    public string FieldDirectionText =>
        FieldAngle switch
        {
            null => "the diagonal",
            0.0 => "the height",
            90.0 => "the width",
            double a => a.ToString("0.###", CultureInfo.InvariantCulture) + " degrees from the height",
        };

    public bool Bounded => Width.HasValue || Height.HasValue;

    public bool Diffracts => PeriodX.HasValue || PeriodY.HasValue;

    private double Lx => PeriodX ?? PeriodY ?? double.PositiveInfinity;
    private double Ly => PeriodY ?? PeriodX ?? double.PositiveInfinity;

    /// <summary>Whether a point of the image plane, in the traced frame, is on the sensor.</summary>
    public bool Covers(double x, double y)
    {
        var (across, along) = ToSensor(x, y);
        return (!Width.HasValue || Math.Abs(across) <= 0.5 * Width.Value) && (!Height.HasValue || Math.Abs(along) <= 0.5 * Height.Value);
    }

    /// <summary>
    /// The change order (m, n) makes to a reflected ray's direction cosines, in a medium of index
    /// <paramref name="medium"/>: m along the sensor's width, n along its height, turned into the
    /// traced frame.
    /// </summary>
    public (double L, double M) Kick(int m, int n, double lambdaUm, double medium)
    {
        double across = m == 0 ? 0.0 : m * lambdaUm / (Math.Abs(medium) * Lx);
        double along = n == 0 ? 0.0 : n * lambdaUm / (Math.Abs(medium) * Ly);
        if (across == 0.0 && along == 0.0) return (0.0, 0.0);
        var (d, e) = Frame;
        return (across * e.X + along * e.Y, across * d.X + along * d.Y);
    }

    /// <summary>
    /// The orders analysed and the fraction of the sensor's reflected light each carries: every
    /// (m, n) up to <see cref="MaxOrder"/> that propagates at normal incidence and carries at
    /// least <see cref="MinimumEfficiency"/>. Without a grating, the zeroth order alone, carrying all.
    /// </summary>
    public IReadOnlyList<(int M, int N, double Efficiency)> Orders(double lambdaUm, double medium)
    {
        if (!Diffracts) return new[] { (0, 0, 1.0) };

        bool Propagates(int m, int n)
        {
            var (l, k) = Kick(m, n, lambdaUm, medium);
            return l * l + k * k < 1.0;
        }

        var list = new List<(int, int, double)>();
        if (Efficiencies != null)
        {
            foreach (var ((m, n), e) in Efficiencies)
                if (Math.Abs(m) <= MaxOrder && Math.Abs(n) <= MaxOrder && Propagates(m, n) && e >= MinimumEfficiency)
                    list.Add((m, n, e));
            return list.OrderBy(o => Math.Abs(o.Item1) + Math.Abs(o.Item2)).ThenBy(o => o.Item1).ThenBy(o => o.Item2).ToList();
        }

        // Normalised over every order that propagates, not only those analysed, so each carries its
        // true share. The count is finite: the grating equation cuts it off.
        int reachX = double.IsInfinity(Lx) ? 0 : (int)Math.Floor(Math.Abs(medium) * Lx / lambdaUm);
        int reachY = double.IsInfinity(Ly) ? 0 : (int)Math.Floor(Math.Abs(medium) * Ly / lambdaUm);
        double total = 0.0;
        for (int m = -reachX; m <= reachX; m++)
            for (int n = -reachY; n <= reachY; n++)
                if (Propagates(m, n)) total += Weight(m, n);

        for (int m = -Math.Min(reachX, MaxOrder); m <= Math.Min(reachX, MaxOrder); m++)
            for (int n = -Math.Min(reachY, MaxOrder); n <= Math.Min(reachY, MaxOrder); n++)
            {
                if (!Propagates(m, n)) continue;
                double e = Weight(m, n) / total;
                if (e >= MinimumEfficiency) list.Add((m, n, e));
            }
        return list.OrderBy(o => Math.Abs(o.Item1) + Math.Abs(o.Item2)).ThenBy(o => o.Item1).ThenBy(o => o.Item2).ToList();
    }

    /// <summary>A pixel's far field: sinc²(m f) sinc²(n f).</summary>
    private double Weight(int m, int n) => Sinc2(m * FillFactor) * Sinc2(n * FillFactor);

    private static double Sinc2(double x)
    {
        if (Math.Abs(x) < 1e-12) return 1.0;
        double s = Math.Sin(Math.PI * x) / (Math.PI * x);
        return s * s;
    }

    /// <summary>Reads a table of measured efficiencies: lines of "m, n, efficiency"; '#' starts a comment.</summary>
    public static Dictionary<(int, int), double> ReadEfficiencies(string path)
    {
        var table = new Dictionary<(int, int), double>();
        foreach (var raw in File.ReadLines(path))
        {
            string line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(new[] { ',', ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double e))
                throw new FormatException($"'{raw}' in {path} is not 'm, n, efficiency'.");
            table[(m, n)] = e;
        }
        return table;
    }

    /// <summary>"(+1,0)": an order as the report writes it.</summary>
    public static string Label(int m, int n) =>
        "(" + (m > 0 ? "+" : "") + m.ToString(CultureInfo.InvariantCulture) + "," + (n > 0 ? "+" : "") + n.ToString(CultureInfo.InvariantCulture) + ")";
}
