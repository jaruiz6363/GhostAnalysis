using System.Collections.Concurrent;
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
///
/// <para>Aimed, each ray is launched where it crosses the ghost's stop at the point the pupil grid
/// names, found by searching the paraxial entrance pupil: a fast lens's pupil aberrations make the
/// paraxial pupil overfill the real stop, and its rim rays would be light the stop does not let in.</para>
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

    /// <summary>
    /// How the rays are aimed at one field: the launch point of the ray through the stop's centre,
    /// and the inverse of the Jacobian there - launch point against where the ray crosses the stop -
    /// the start of every other ray's search.
    /// </summary>
    private sealed record Aim(double Y0, double X0, double Iyy, double Iyx, double Ixy, double Ixx);

    // The paraxial marginal ray's height at the ghost's stop, signed: a pupil coordinate of 1 is a
    // ray through the stop there. 0 when the rays are not aimed.
    private readonly double _stopHeight;
    private readonly ConcurrentDictionary<double, Aim?> _aims = new();
    private readonly ConcurrentDictionary<double, double> _areas = new();
    // The ghost as far as its stop, where the search for an aimed ray traces it; null to trace it all.
    private readonly OpticalSystem? _toStop;
    private readonly double[]? _toStopN;

    /// <param name="layout">The unfolded ghost.</param>
    /// <param name="n">Its indices, unsigned, as IndexResolver gives them.</param>
    /// <param name="p">Its paraxial trace, which places its entrance pupil.</param>
    /// <param name="sd">Each layout surface's semi-diameter, 0 for none; checked when clipping.</param>
    /// <param name="ghostStop">The ghost's stop, which the pupil grid fills and is not checked; -1 for none.</param>
    /// <param name="onSensor">Which layout surfaces are reflections from the sensor.</param>
    /// <param name="kicks">The direction-cosine kick at each of those, in the order the light meets them; null for none.</param>
    /// <param name="aim">Whether to aim each ray at the stop (<see cref="GhostOptions.AimRays"/>).</param>
    public GhostTracer(OpticalSystem layout, double[] n, ParaxialResult p, double[] sd, int ghostStop,
                       bool[] onSensor, Sensor? sensor, IReadOnlyList<(double L, double M)>? kicks, bool aim = false)
    {
        _layout = layout;
        _n = n;
        _p = p;
        _sd = sd;
        _ghostStop = ghostStop;
        _onSensor = onSensor;
        _sensor = sensor;
        _stopHeight = aim && ghostStop > 0 ? p.Y[ghostStop] : 0.0;

        var mirrors = Enumerable.Range(0, onSensor.Length).Where(j => onSensor[j]).ToList();
        var kicked = new List<(int Mirror, double L, double M)>();
        for (int k = 0; k < mirrors.Count; k++)
        {
            var kick = kicks != null && k < kicks.Count ? kicks[k] : (0.0, 0.0);
            if (kick.Item1 != 0.0 || kick.Item2 != 0.0) kicked.Add((mirrors[k], kick.Item1, kick.Item2));
        }
        _segments = Split(layout, n, kicked);

        // The search for each aimed ray traces it only to the stop, where the stop comes before any
        // grating turns it: the ghost's surfaces as far as the stop, and a plane behind.
        if (_stopHeight != 0.0 && (kicked.Count == 0 || ghostStop < kicked[0].Mirror))
        {
            var surfaces = layout.Surfaces.Take(ghostStop + 1).Append(new Surface { Thickness = 0.0 }).ToList();
            _toStop = new OpticalSystem { FieldType = layout.FieldType, Surfaces = surfaces };
            _toStopN = n.Take(ghostStop + 1).Append(n[ghostStop]).ToArray();
        }
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
        if (_stopHeight == 0.0) return Launch(field, py, px, clip, visit);
        var at = Aimed(field, py, px);
        return at is (double ly, double lx) ? Launch(field, ly, lx, clip, visit) : null;
    }

    /// <summary>Whether the rays are aimed at the stop, rather than launched across the paraxial pupil.</summary>
    public bool Aims => _stopHeight != 0.0;

    /// <summary>
    /// The area of the entrance pupil the aimed rays fill at one field - the part of the incoming beam
    /// that passes through the stop - as a fraction of the paraxial pupil's: the real pupil's rim,
    /// found ray by ray. 1 when the rays are not aimed.
    /// </summary>
    public double PupilArea(double field)
    {
        if (_stopHeight == 0.0) return 1.0;
        return _areas.GetOrAdd(field, h =>
        {
            // The polygon through the real rim's points, against the same polygon through the paraxial
            // rim's: the corners it cuts off, the paraxial polygon cuts off alike.
            const int rim = 32;
            var points = new List<(double Y, double X)>();
            for (int k = 0; k < rim; k++)
            {
                double t = 2 * Math.PI * k / rim;
                if (Aimed(h, Math.Cos(t), Math.Sin(t)) is { } p) points.Add(p);
            }
            if (points.Count < 3) return 1.0;
            double area = 0.0;
            for (int k = 0; k < points.Count; k++)
            {
                var (y1, x1) = points[k];
                var (y2, x2) = points[(k + 1) % points.Count];
                area += x1 * y2 - x2 * y1;
            }
            return Math.Abs(area) / 2.0 / (rim / 2.0 * Math.Sin(2 * Math.PI / rim));
        });
    }

    /// <summary>
    /// Where in the paraxial entrance pupil to launch the ray that crosses the ghost's stop at
    /// (<paramref name="px"/>, <paramref name="py"/>) times the paraxial marginal ray's height there:
    /// Newton's method, from the chief ray's Jacobian, taking a fresh one where that converges slowly.
    /// Null where no ray gets there.
    /// </summary>
    private (double Y, double X)? Aimed(double field, double py, double px)
    {
        var aim = _aims.GetOrAdd(field, AimAt);
        if (aim == null) return (py, px);            // the stop's centre out of reach: launched paraxially
        double ty = py * _stopHeight, tx = px * _stopHeight;
        double ly = aim.Y0 + aim.Iyy * ty + aim.Iyx * tx;
        double lx = aim.X0 + aim.Ixy * ty + aim.Ixx * tx;
        return Solve(field, ty, tx, ly, lx, aim);
    }

    /// <summary>The chief ray's launch point at one field, and the Jacobian there; null if no ray reaches the stop's centre.</summary>
    private Aim? AimAt(double field)
    {
        var start = Jacobian(field, 0.0, 0.0);
        if (start == null) return null;
        var chief = Solve(field, 0.0, 0.0, 0.0, 0.0, start);
        if (chief is not (double y0, double x0)) return null;
        var j = Jacobian(field, y0, x0);
        return j == null ? null : j with { Y0 = y0, X0 = x0 };
    }

    /// <summary>
    /// The launch point whose ray crosses the stop at (tx, ty), from (lx, ly): Broyden's method,
    /// its inverse Jacobian that of <paramref name="j"/> to start with and bettered by each step,
    /// one trace a step; a fresh one taken by differences if it stalls. To a billionth of the stop's radius.
    /// </summary>
    private (double Y, double X)? Solve(double field, double ty, double tx, double ly, double lx, Aim j)
    {
        double tolerance = 1e-9 * Math.Abs(_stopHeight);
        double hyy = j.Iyy, hyx = j.Iyx, hxy = j.Ixy, hxx = j.Ixx;
        double last = double.PositiveInfinity, fy = 0, fx = 0, dy = 0, dx = 0;
        for (int k = 0; k < 40; k++)
        {
            if (StopHit(field, ly, lx) is not (double sy, double sx)) return null;
            double ry = sy - ty, rx = sx - tx;
            double r = Math.Sqrt(ry * ry + rx * rx);
            if (r <= tolerance) return (ly, lx);
            if (r > 0.5 * last && Jacobian(field, ly, lx) is { } fresh)
            {
                (hyy, hyx, hxy, hxx) = (fresh.Iyy, fresh.Iyx, fresh.Ixy, fresh.Ixx);
            }
            else if (k > 0)
            {
                // Broyden's good update of the inverse: H += (Δx - H Δf) (Δxᵀ H) / (Δxᵀ H Δf).
                double gy = ry - fy, gx = rx - fx;                              // Δf
                double hgy = hyy * gy + hyx * gx, hgx = hxy * gy + hxx * gx;    // H Δf
                double denom = dy * hgy + dx * hgx;
                if (Math.Abs(denom) > 1e-300)
                {
                    double uy = (dy - hgy) / denom, ux = (dx - hgx) / denom;
                    double vy = dy * hyy + dx * hxy, vx = dy * hyx + dx * hxx;  // Δxᵀ H
                    hyy += uy * vy; hyx += uy * vx; hxy += ux * vy; hxx += ux * vx;
                }
            }
            last = r;
            fy = ry; fx = rx;
            dy = -(hyy * ry + hyx * rx);
            dx = -(hxy * ry + hxx * rx);
            ly += dy;
            lx += dx;
        }
        return null;
    }

    /// <summary>The inverse Jacobian at one launch point, by central differences; null where it cannot be taken.</summary>
    private Aim? Jacobian(double field, double ly, double lx)
    {
        const double d = 1e-5;
        if (StopHit(field, ly + d, lx) is not (double y1, double x1) || StopHit(field, ly - d, lx) is not (double y2, double x2) ||
            StopHit(field, ly, lx + d) is not (double y3, double x3) || StopHit(field, ly, lx - d) is not (double y4, double x4))
            return null;
        double a = (y1 - y2) / (2 * d), c = (x1 - x2) / (2 * d);      // d(stop y, stop x) / d(launch y)
        double b = (y3 - y4) / (2 * d), e = (x3 - x4) / (2 * d);      // d(stop y, stop x) / d(launch x)
        double det = a * e - b * c;
        if (!double.IsFinite(det) || Math.Abs(det) < 1e-300) return null;
        return new Aim(ly, lx, e / det, -b / det, -c / det, a / det);
    }

    /// <summary>
    /// A ray a whisker from the chief ray, unclipped, for the foci: aimed, launched where the chief
    /// ray's Jacobian puts it - so near the chief ray, that is where it crosses the stop to a part
    /// in a million of the whisker, and no search is needed.
    /// </summary>
    private RealRayTrace.SurfaceHit? Beside(double field, double py, double px)
    {
        if (_stopHeight == 0.0 || _aims.GetOrAdd(field, AimAt) is not { } aim) return Launch(field, py, px, false, null);
        double ty = py * _stopHeight, tx = px * _stopHeight;
        return Launch(field, aim.Y0 + aim.Iyy * ty + aim.Iyx * tx, aim.X0 + aim.Ixy * ty + aim.Ixx * tx, false, null);
    }

    /// <summary>Where a ray launched at (lx, ly) crosses the ghost's stop, in the stop's own frame; null if it does not.</summary>
    private (double Y, double X)? StopHit(double field, double ly, double lx)
    {
        (double, double)? at = null;
        try
        {
            if (_toStop != null)
            {
                // Only as far as the stop.
                var hits = RealRayTrace.TraceRecord(_toStop, _toStopN!, _p, field, ly, lx, atParaxialFocus: false);
                for (int i = 1; i <= _ghostStop; i++)
                    if (!hits[i].Ok) return null;
                return (hits[_ghostStop].Y, hits[_ghostStop].X);
            }
            Launch(field, ly, lx, clip: false, (j, h) => { if (j == _ghostStop) at = (h.Y, h.X); });
        }
        catch (InvalidOperationException) { return null; }
        return at;
    }

    /// <summary>The trace itself, from a launch point in the paraxial entrance pupil.</summary>
    private RealRayTrace.SurfaceHit? Launch(double field, double py, double px, bool clip,
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
                // Every surface but the ghost's own stop, which the pupil grid is what fills: aimed, its
                // rim rays cross the stop at its rim exactly, and a test there would only split
                // hairs on them. (Unaimed, the grid fills the paraxial pupil, and the stop is not
                // checked either: see docs/verification.md.)
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
        var up = Beside(field, d, 0.0);
        var down = Beside(field, -d, 0.0);
        var right = Beside(field, 0.0, d);
        var left = Beside(field, 0.0, -d);

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
