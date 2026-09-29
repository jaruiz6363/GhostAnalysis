using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// Real rays through one unfolded ghost, stopped where the ghost's apertures and the sensor's
/// edges stop them, and turned into their diffraction order at every reflection from the sensor.
///
/// <para>AberrationCalculator's trace knows mirrors but not gratings, so a ghost whose sensor
/// reflections diffract is traced in segments, split at each of them: the ray is traced to the
/// sensor, its reflected direction given the grating's kick there, and it is carried to the next
/// surface's vertex plane and traced on from there. Without a kick the ghost is one segment and
/// this is AberrationCalculator's trace unchanged.</para>
/// </summary>
public sealed class GhostTracer
{
    private sealed record Segment(OpticalSystem System, double[] N, int Offset, int Mirror,
                                  double KickL, double KickM);

    private readonly OpticalSystem _layout;
    private readonly double[] _n;
    private readonly ParaxialResult _p;
    private readonly double[] _sd;
    private readonly int _ghostStop;
    private readonly bool[] _onSensor;
    private readonly Sensor? _sensor;
    private readonly Segment[] _segments;

    /// <param name="layout">The unfolded ghost.</param>
    /// <param name="n">Its indices, unsigned, as IndexResolver gives them.</param>
    /// <param name="p">Its paraxial trace, which places its entrance pupil.</param>
    /// <param name="sd">Each layout surface's semi-diameter, 0 for none; checked when clipping.</param>
    /// <param name="ghostStop">The ghost's stop, which the pupil grid fills and is not checked; -1 for none.</param>
    /// <param name="onSensor">Which layout surfaces are reflections from the sensor.</param>
    /// <param name="kicks">The direction-cosine kick at each of those, in the order the light meets them; null for none.</param>
    public GhostTracer(OpticalSystem layout, double[] n, ParaxialResult p, double[] sd, int ghostStop,
                       bool[] onSensor, Sensor? sensor, IReadOnlyList<(double L, double M)>? kicks)
    {
        _layout = layout;
        _n = n;
        _p = p;
        _sd = sd;
        _ghostStop = ghostStop;
        _onSensor = onSensor;
        _sensor = sensor;

        var mirrors = Enumerable.Range(0, onSensor.Length).Where(j => onSensor[j]).ToList();
        var kicked = new List<(int Mirror, double L, double M)>();
        for (int k = 0; k < mirrors.Count; k++)
        {
            var kick = kicks != null && k < kicks.Count ? kicks[k] : (0.0, 0.0);
            if (kick.Item1 != 0.0 || kick.Item2 != 0.0) kicked.Add((mirrors[k], kick.Item1, kick.Item2));
        }
        _segments = Split(layout, n, kicked);
    }

    /// <summary>A tracer that neither clips nor diffracts: the ghost's rays as they go.</summary>
    public static GhostTracer Plain(OpticalSystem layout, double[] n, ParaxialResult p) =>
        new(layout, n, p, new double[layout.Surfaces.Count], -1, new bool[layout.Surfaces.Count], null, null);

    /// <summary>
    /// The segments: from the object to the first kicked reflection, from there to the next, and
    /// the last to the image. Each is a system of its own whose surfaces are the layout's (shared,
    /// not copied), with a dummy object in front after a reflection and a dummy image behind before one.
    /// </summary>
    private static Segment[] Split(OpticalSystem layout, double[] n, List<(int Mirror, double L, double M)> kicked)
    {
        int last = layout.Surfaces.Count - 1;
        if (kicked.Count == 0) return new[] { new Segment(layout, n, 0, -1, 0, 0) };

        var segments = new List<Segment>();
        int from = 0;
        for (int k = 0; k <= kicked.Count; k++)
        {
            int to = k < kicked.Count ? kicked[k].Mirror : last;
            var surfaces = new List<Surface>();
            var idx = new List<double>();
            if (from == 0) { surfaces.Add(layout.Surfaces[0]); idx.Add(n[0]); }
            else { surfaces.Add(new Surface { Thickness = 0.0 }); idx.Add(n[from]); }
            int start = from == 0 ? 1 : from + 1;
            for (int j = start; j <= to; j++) { surfaces.Add(layout.Surfaces[j]); idx.Add(n[j]); }
            if (to != last) { surfaces.Add(new Surface { Thickness = 0.0 }); idx.Add(n[to]); }

            var sys = new OpticalSystem { FieldType = layout.FieldType, Surfaces = surfaces };
            int offset = from == 0 ? 0 : from;
            var (l, m) = k > 0 ? (kicked[k - 1].L, kicked[k - 1].M) : (0.0, 0.0);
            segments.Add(new Segment(sys, idx.ToArray(), offset, from == 0 ? -1 : from, l, m));
            from = to;
        }
        return segments.ToArray();
    }

    /// <summary>
    /// Traces one ray, launched at the given field through the given point of the ghost's entrance
    /// pupil, to the sensor: where it lands and which way it is going, or null if it does not
    /// arrive. Clipping, it is stopped outside any semi-diameter, and on the image plane outside
    /// the sensor - where it lands, and where it reflects from it.
    /// </summary>
    public RealRayTrace.SurfaceHit? Trace(double field, double py, double px, bool clip) =>
        Trace(field, py, px, clip, null);

    /// <summary>
    /// <see cref="Trace(double, double, double, bool)"/>, reporting every surface the ray meets on
    /// the way, by its index in the layout, with where it met it in that surface's own frame -
    /// the one that stops it included, so a drawing can show where the light is lost.
    /// </summary>
    public RealRayTrace.SurfaceHit? Trace(double field, double py, double px, bool clip,
                                          Action<int, RealRayTrace.SurfaceHit>? visit)
    {
        RealRayTrace.SurfaceHit end = default;
        double x = 0, y = 0, z = 0, l = 0, m = 0, nz = 0;
        for (int s = 0; s < _segments.Length; s++)
        {
            var seg = _segments[s];
            RealRayTrace.SurfaceHit[] hits;
            if (s == 0)
            {
                hits = RealRayTrace.TraceRecord(seg.System, seg.N, _p, field, py, px, atParaxialFocus: false);
            }
            else
            {
                // Off the sensor in its mirror direction, turned by the grating into this order.
                l += seg.KickL;
                m += seg.KickM;
                double nn = 1.0 - l * l - m * m;
                if (nn <= 0.0) return null;                         // an order that does not propagate
                nz = Math.Sign(nz) * Math.Sqrt(nn);
                // On to the vertex plane of the next surface, and traced from there.
                double t = _layout.Surfaces[seg.Mirror].Thickness;
                double run = (t - z) / nz;
                hits = RealRayTrace.TraceRecordFrom(seg.System, seg.N, _p, x + run * l, y + run * m, l, m, nz,
                                                    atParaxialFocus: false);
            }

            bool lastSegment = s == _segments.Length - 1;
            int through = lastSegment ? hits.Length - 1 : hits.Length - 2;     // the kicked mirror, or the image
            for (int i = 1; i <= through; i++)
            {
                var h = hits[i];
                if (!h.Ok) return null;
                int j = seg.Offset + i;
                visit?.Invoke(j, h);
                bool image = lastSegment && i == hits.Length - 1;
                if (!clip) continue;
                if (image || _onSensor[j])
                {
                    if (_sensor != null && !_sensor.Covers(h.X, h.Y)) return null;
                }
                else if (j != _ghostStop && _sd[j] > 0 && Math.Sqrt(h.X * h.X + h.Y * h.Y) > _sd[j] * (1 + 1e-9))
                    return null;
            }
            end = hits[through];
            x = end.X; y = end.Y; z = end.Z; l = end.L; m = end.M; nz = end.N;
        }
        return end;
    }

    /// <summary>
    /// Where the ghost's real light comes to a focus at one field, from the sensor, positive when
    /// short of it: the crossing of two real rays either side of the chief ray - in the field's
    /// plane for the tangential focus, across it for the sagittal - Coddington's foci, traced.
    /// NaN where the rays do not arrive or run parallel.
    /// </summary>
    public (double Tangential, double Sagittal) Foci(double field)
    {
        const double d = 1e-3;
        var up = Trace(field, d, 0.0, clip: false);
        var down = Trace(field, -d, 0.0, clip: false);
        var right = Trace(field, 0.0, d, clip: false);
        var left = Trace(field, 0.0, -d, clip: false);

        // Two rays at heights a, b on the sensor with slopes ta, tb meet at z = -(a - b)/(ta - tb)
        // from it; short of it is -z.
        static double Meet(double a, double b, double ta, double tb) =>
            Math.Abs(ta - tb) > 1e-15 ? (a - b) / (ta - tb) : double.PositiveInfinity;

        double t = up is { } u && down is { } w ? Meet(u.Y, w.Y, u.M / u.N, w.M / w.N) : double.NaN;
        double s = right is { } r && left is { } q ? Meet(r.X, q.X, r.L / r.N, q.L / q.N) : double.NaN;
        return (t, s);
    }

    /// <summary>
    /// The fields between <paramref name="from"/> and <paramref name="to"/> at which the ghost's real
    /// tangential or sagittal focus is on the sensor: a fine scan for a change of sign, each then
    /// closed by bisection. A focus also changes sign by passing through infinity, where the rays
    /// leave parallel; that is no crossing, and is told apart by the focus growing there instead
    /// of vanishing.
    /// </summary>
    public IReadOnlyList<(double Field, string Kind)> Crossings(double from, double to, int steps = 120)
    {
        var found = new List<(double, string)>();
        var foci = new (double T, double S)[steps + 1];
        var h = new double[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            h[i] = from + (to - from) * i / steps;
            foci[i] = Foci(h[i]);
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
                    double fm = pick(Foci(mid));
                    if (!double.IsFinite(fm)) { ok = false; break; }
                    if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid;
                }
                double at = 0.5 * (lo + hi);
                double end = pick(Foci(at));
                if (ok && double.IsFinite(end) && Math.Abs(end) < 1e-6 * (Math.Abs(a) + Math.Abs(b)))
                    found.Add((at, kind));
            }
        }
        return found.OrderBy(x => x.Item1).ToList();
    }
}
