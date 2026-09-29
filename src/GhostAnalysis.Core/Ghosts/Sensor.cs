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

    public bool Bounded => Width.HasValue || Height.HasValue;

    public bool Diffracts => PeriodX.HasValue || PeriodY.HasValue;

    private double Lx => PeriodX ?? PeriodY ?? double.PositiveInfinity;
    private double Ly => PeriodY ?? PeriodX ?? double.PositiveInfinity;

    /// <summary>Whether a point of the image plane is on the sensor.</summary>
    public bool Covers(double x, double y) =>
        (!Width.HasValue || Math.Abs(x) <= 0.5 * Width.Value) && (!Height.HasValue || Math.Abs(y) <= 0.5 * Height.Value);

    /// <summary>The change order (m, n) makes to a reflected ray's direction cosines, in a medium of index <paramref name="n"/>.</summary>
    public (double L, double M) Kick(int m, int n, double lambdaUm, double medium) =>
        (m == 0 ? 0.0 : m * lambdaUm / (Math.Abs(medium) * Lx), n == 0 ? 0.0 : n * lambdaUm / (Math.Abs(medium) * Ly));

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
