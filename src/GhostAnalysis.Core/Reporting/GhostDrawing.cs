using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using GhostAnalysis.Core.Ghosts;

namespace GhostAnalysis.Core.Reporting;

/// <summary>
/// A ghost drawn in the lens: the lens in section, and a fan of the ghost's real rays through it,
/// forward, back and forward again, each leg between reflections in a colour of its own.
///
/// <para>The drawing of the lens - each glass closed by its two faces out to their own clear
/// apertures, the larger face at its full sag and the smaller extended to meet it; the stop's
/// bars; the image plane; the scale bar - is ported from LensHH-LT's SystemLayoutRenderer
/// (MIT licence, copyright (c) 2026 Synapse Optics), from its own layout model to
/// AberrationCalculator's lens.</para>
///
/// <para>The rays are the ghost's, traced by its <see cref="GhostTracer"/> through the unfolded
/// ghost and folded back into the lens: each point is where the ray meets a lens surface, at that
/// surface's place along the axis. A ray the ghost's apertures or the sensor stop is drawn faintly
/// to where it is stopped, so the drawing shows where the ghost's light is lost as well as where
/// it goes.</para>
/// </summary>
public static class GhostDrawing
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;
    private static string F(FormattableString s) => s.ToString(C);

    /// <summary>The ghost's legs: in, after the first reflection, after the second, ...</summary>
    private static readonly string[] LegColors = { "#1f77b4", "#d62728", "#2ca02c", "#ff7f0e", "#9467bd", "#8c564b" };

    private const string GlassFill = "#B8D4F0", GlassStroke = "#4477AA", Reflecting = "#d62728";

    /// <summary>One ray of the fan: its legs, split at each reflection, and whether it reached the sensor.</summary>
    private sealed record Ray(List<List<(double Z, double Y)>> Legs, bool Arrived);

    /// <param name="rank">The ghost's place among the brightest, for the title; 0 for none.</param>
    /// <param name="field">The field to draw at; by default the ghost's brightest.</param>
    public static string Svg(GhostResult r, Ghost g, int rank = 0, double? field = null, int rays = 13,
                             int width = 900, int height = 460)
    {
        var lens = r.Lens;
        int image = lens.Surfaces.Count - 1;
        int lastLens = image - 1;
        double h = field ?? g.Peak?.Field ?? 0.0;

        // Where each lens surface stands along the axis, surface 1 at zero.
        var z = new double[lens.Surfaces.Count];
        for (int i = 2; i <= image; i++) z[i] = z[i - 1] + Finite(lens.Surfaces[i - 1].Thickness);
        double track = Math.Max(z[image], 1e-6);
        double lead = Math.Max(track * 0.15, 5.0);

        // The ghost's fan, in the plane of the field, folded into the lens.
        var fan = new List<Ray>();
        for (int k = 0; k < rays; k++)
        {
            double py = rays == 1 ? 0.0 : -1.0 + 2.0 * k / (rays - 1);
            var legs = new List<List<(double, double)>> { new() };
            bool first = true;
            var end = g.Tracer.Trace(h, py, 0.0, clip: true, (j, hit) =>
            {
                int s = g.Layout.Origin[j];
                var point = (z[s] + hit.Z, hit.Y);
                if (first)
                {
                    legs[0].Add(LeadIn(lens, h, point, lead));
                    first = false;
                }
                legs[^1].Add(point);
                if (g.Layout.Reflects[j]) legs.Add(new List<(double, double)> { point });
            });
            if (!first) fan.Add(new Ray(legs, end != null));
        }

        // The lens's own rays at the same field, for reference: the edges of its beam and its chief ray.
        var own = new List<List<(double Z, double Y)>>();
        var n = r.NominalIndices.ToArray();
        foreach (double py in new[] { -1.0, 0.0, 1.0 })
        {
            var hits = RealRayTrace.TraceRecord(lens, n, r.Nominal, h, py, 0.0, atParaxialFocus: false);
            var line = new List<(double, double)>();
            for (int i = 1; i <= image && hits[i].Ok; i++)
            {
                var point = (z[i] + hits[i].Z, hits[i].Y);
                if (i == 1) line.Add(LeadIn(lens, h, point, lead));
                line.Add(point);
            }
            if (line.Count > 1) own.Add(line);
        }

        // Each surface's aperture, for the glass: its own, or where it has none the beam's.
        var sd = new double[lens.Surfaces.Count];
        for (int i = 1; i <= lastLens; i++)
            sd[i] = lens.Surfaces[i].SemiDiameter > 0 ? lens.Surfaces[i].SemiDiameter
                  : 1.1 * Math.Max(Math.Abs(r.Nominal.Y[i]), own.SelectMany(l => l).Where(p => Math.Abs(p.Z - z[i]) < 1e-6).Select(p => Math.Abs(p.Y)).DefaultIfEmpty(0).Max());

        // The sensor, and where the ghost comes to a focus.
        // Without a size given, the sensor is drawn to the image of the largest field analysed, and
        // never smaller than a third of the lens: an on-axis drawing's rays all land at the centre.
        double reach = r.Options.Sensor.HalfExtentAlongField;
        double sensorHalf = double.IsFinite(reach) ? reach
            : Math.Max(g.Fields.Select(f => Math.Abs(f.ImageHeight)).Where(double.IsFinite).DefaultIfEmpty(0).Max(),
                       sd.Max() / 3.0);
        double focusZ = z[lastLens] + g.ImageDistance;
        bool focusShown = double.IsFinite(focusZ) && focusZ > -lead - 0.5 * track && focusZ < 1.5 * track;

        // The viewport: every surface, every ray, the lead-in and the ghost's focus if near.
        double minZ = -lead, maxZ = z[image];
        if (focusShown) { minZ = Math.Min(minZ, focusZ); maxZ = Math.Max(maxZ, focusZ); }
        double maxY = Math.Max(sd.Max(), sensorHalf);
        foreach (var p in fan.SelectMany(f => f.Legs).SelectMany(l => l).Concat(own.SelectMany(l => l)))
        {
            minZ = Math.Min(minZ, p.Z);
            maxZ = Math.Max(maxZ, p.Z);
            maxY = Math.Max(maxY, Math.Abs(p.Y));
        }

        int marginX = 60, marginY = 70;
        double plotW = width - 2 * marginX, plotH = height - 2 * marginY;
        double spanZ = (maxZ - minZ) * 1.06;
        double scale = Math.Min(plotW / spanZ, plotH / (2.2 * maxY));
        double xOffset = marginX + (plotW - (maxZ - minZ) * scale) / 2.0;
        double centerY = marginY + plotH / 2.0 + 10;
        double X(double zz) => xOffset + (zz - minZ) * scale;
        double Y(double yy) => centerY - yy * scale;

        var sb = new StringBuilder();
        sb.AppendLine(F($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"sans-serif\">"));
        sb.AppendLine(F($"<rect width=\"{width}\" height=\"{height}\" fill=\"white\"/>"));

        string unit = lens.FieldType == FieldType.ObjectAngle ? "°" : "";
        string along = r.Options.Sensor.Bounded || r.Options.Sensor.Diffracts ? " along " + r.Options.Sensor.FieldDirectionText : "";
        string title = (rank > 0 ? $"#{rank}: " : "") + g.Name + F($" at field {h:0.###}{unit}") + along + (string.IsNullOrWhiteSpace(lens.Title) ? "" : " - " + lens.Title);
        sb.AppendLine(F($"<text x=\"{width / 2}\" y=\"20\" text-anchor=\"middle\" font-size=\"13\" font-weight=\"bold\">{Esc(title)}</text>"));

        // The axis.
        sb.AppendLine(F($"<line x1=\"{X(minZ):F1}\" y1=\"{centerY:F1}\" x2=\"{X(maxZ):F1}\" y2=\"{centerY:F1}\" stroke=\"#999\" stroke-width=\"0.5\" stroke-dasharray=\"4,2\"/>"));

        // The glass.
        for (int i = 1; i < lastLens; i++)
        {
            if (Math.Abs(n[i] - 1.0) < 1e-6 || lens.Surfaces[i].IsMirror) continue;
            sb.AppendLine(F($"<path d=\"{Element(lens.Surfaces[i], lens.Surfaces[i + 1], z[i], z[i + 1], sd[i], sd[i + 1], X, Y)}\" fill=\"{GlassFill}\" stroke=\"{GlassStroke}\" stroke-width=\"0.8\" opacity=\"0.7\"/>"));
        }

        // The surfaces the ghost reflects from, labelled - a label that would sit on another is lifted above it.
        var labels = new List<(double X, double Y)>();
        foreach (int s in g.Path.Surfaces.Distinct().OrderBy(s => s))
        {
            if (s == image) continue;
            sb.AppendLine(F($"<path d=\"{Profile(lens.Surfaces[s], z[s], sd[s], X, Y)}\" fill=\"none\" stroke=\"{Reflecting}\" stroke-width=\"2.5\"/>"));
            double lx = X(z[s]), ly = Y(sd[s]) - 6;
            while (labels.Any(l => Math.Abs(l.X - lx) < 22 && Math.Abs(l.Y - ly) < 11)) ly -= 12;
            labels.Add((lx, ly));
            sb.AppendLine(F($"<text x=\"{lx:F1}\" y=\"{ly:F1}\" text-anchor=\"middle\" font-size=\"10\" fill=\"{Reflecting}\">S{s}</text>"));
        }

        // The stop.
        int stop = lens.StopSurfaceIndex;
        if (stop >= 1 && stop <= lastLens)
        {
            double r0 = (lens.Surfaces[stop].SemiDiameter > 0 ? lens.Surfaces[stop].SemiDiameter : Math.Abs(r.Nominal.Y[stop])) * scale;
            double sx = X(z[stop]);
            sb.AppendLine(F($"<line x1=\"{sx:F1}\" y1=\"{centerY - r0 - 6:F1}\" x2=\"{sx:F1}\" y2=\"{centerY - r0:F1}\" stroke=\"black\" stroke-width=\"1.5\"/>"));
            sb.AppendLine(F($"<line x1=\"{sx:F1}\" y1=\"{centerY + r0:F1}\" x2=\"{sx:F1}\" y2=\"{centerY + r0 + 6:F1}\" stroke=\"black\" stroke-width=\"1.5\"/>"));
        }

        // The sensor: red when the ghost reflects from it.
        bool sensorReflects = g.Path.Surfaces.Contains(image);
        double ix = X(z[image]);
        sb.AppendLine(F($"<line x1=\"{ix:F1}\" y1=\"{Y(sensorHalf):F1}\" x2=\"{ix:F1}\" y2=\"{Y(-sensorHalf):F1}\" stroke=\"{(sensorReflects ? Reflecting : "black")}\" stroke-width=\"{(sensorReflects ? 2.5 : 1.2)}\"/>"));
        // Labelled below, clear of the image label and of reflecting-surface labels above.
        sb.AppendLine(F($"<text x=\"{ix:F1}\" y=\"{Y(-sensorHalf) + 13:F1}\" text-anchor=\"middle\" font-size=\"10\" fill=\"{(sensorReflects ? Reflecting : "#333")}\">sensor</text>"));

        // The lens's own rays, faint and dashed, so they are not taken for the ghost's, and where
        // they form the image.
        foreach (var line in own)
            sb.AppendLine(F($"<polyline points=\"{Points(line, X, Y)}\" fill=\"none\" stroke=\"#bbbbbb\" stroke-width=\"0.8\" stroke-dasharray=\"5,3\"/>"));
        double? imageY = own.Count > 0 ? own[own.Count / 2][^1].Y : null;       // the chief ray
        if (imageY is double iy0)
        {
            double iy = Y(iy0);
            sb.AppendLine(F($"<circle cx=\"{ix:F1}\" cy=\"{iy:F1}\" r=\"2.5\" fill=\"none\" stroke=\"#888\" stroke-width=\"1\"/>"));
            sb.AppendLine(F($"<text x=\"{ix + 6:F1}\" y=\"{iy + 3:F1}\" font-size=\"10\" fill=\"#888\">image</text>"));
        }
        bool imageOff = imageY is double iy1 && double.IsFinite(reach) && Math.Abs(iy1) > reach;

        // The ghost's rays: those that reach the sensor strong, those stopped faint, ending in a cross.
        foreach (var ray in fan)
        {
            double opacity = ray.Arrived ? 0.9 : 0.3;
            for (int leg = 0; leg < ray.Legs.Count; leg++)
            {
                if (ray.Legs[leg].Count < 2) continue;
                string color = LegColors[leg % LegColors.Length];
                sb.AppendLine(F($"<polyline points=\"{Points(ray.Legs[leg], X, Y)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"0.8\" opacity=\"{opacity:0.##}\"/>"));
            }
            if (!ray.Arrived)
            {
                var (ez, ey) = ray.Legs.Last(l => l.Count > 0)[^1];
                double mx = X(ez), my = Y(ey);
                sb.AppendLine(F($"<path d=\"M {mx - 2.5:F1},{my - 2.5:F1} L {mx + 2.5:F1},{my + 2.5:F1} M {mx - 2.5:F1},{my + 2.5:F1} L {mx + 2.5:F1},{my - 2.5:F1}\" stroke=\"#555\" stroke-width=\"0.8\" opacity=\"0.6\"/>"));
            }
        }

        // The ghost's paraxial focus.
        if (focusShown)
        {
            double fx = X(focusZ);
            sb.AppendLine(F($"<line x1=\"{fx:F1}\" y1=\"{centerY - 9:F1}\" x2=\"{fx:F1}\" y2=\"{centerY + 9:F1}\" stroke=\"#6a1b9a\" stroke-width=\"1.5\"/>"));
            sb.AppendLine(F($"<text x=\"{fx:F1}\" y=\"{centerY + 22:F1}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#6a1b9a\">ghost focus</text>"));
        }

        Panels(sb, r, g, h, fan, width, sensorReflects, focusShown,
               imageOff ? F($"The lens's image ({Math.Abs(imageY!.Value):0.##}) is off the sensor (edge at {reach:0.##})") : null);
        ScaleBar(sb, scale, width, height, marginY, track, r.Wavelength);
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>The ghosts' drawings on one page.</summary>
    public static string Page(GhostResult r, IReadOnlyList<Ghost> ghosts, double? field = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Ghosts: {Esc(r.Lens.Title)}</title>");
        sb.AppendLine("<style>body{font-family:sans-serif;margin:20px;} svg{border:1px solid #ccc;margin-bottom:16px;display:block;}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h2>The {ghosts.Count} brightest ghosts: {Esc(r.Lens.Title)}</h2>");
        for (int i = 0; i < ghosts.Count; i++) sb.AppendLine(Svg(r, ghosts[i], i + 1, field));
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Where a ray comes from, before the first surface: back along its direction by
    /// <paramref name="lead"/> - parallel at the field angle from infinity, from the object point otherwise.
    /// </summary>
    private static (double Z, double Y) LeadIn(OpticalSystem lens, double field, (double Z, double Y) first, double lead)
    {
        double t0 = lens.Surfaces[0].Thickness;
        double slope;
        if (double.IsInfinity(t0) || Math.Abs(t0) >= 1e12)
            slope = Math.Tan(field * Math.PI / 180.0);
        else
        {
            double height = lens.FieldType == FieldType.ObjectHeight ? field : -Math.Tan(field * Math.PI / 180.0) * Math.Abs(t0);
            slope = (first.Y - height) / (first.Z + Math.Abs(t0));
            lead = Math.Min(lead, first.Z + Math.Abs(t0));
        }
        return (first.Z - lead, first.Y - slope * lead);
    }

    /// <summary>
    /// One glass in section, by LensHH-LT's rim method: the face with the larger clear aperture
    /// drawn at its full sag, the smaller one to its own aperture and then straight across to meet
    /// the larger's rim, so a lens whose faces differ in size closes as it is made.
    /// </summary>
    private static string Element(Surface front, Surface back, double zf, double zb, double sdF, double sdB,
                                  Func<double, double> X, Func<double, double> Y)
    {
        double FrontZ(double y) => zf + Sag(front, y, sdF);
        double BackZ(double y) => zb + Sag(back, y, sdB);
        const int nPts = 51;
        var d = new StringBuilder();

        for (int i = 0; i <= nPts; i++)
        {
            double y = sdF * (1.0 - 2.0 * i / nPts);
            d.Append(i == 0 ? F($"M {X(FrontZ(y)):F2},{Y(y):F2}") : F($" L {X(FrontZ(y)):F2},{Y(y):F2}"));
        }
        if (sdF >= sdB)
        {
            d.Append(F($" L {X(BackZ(-sdB)):F2},{Y(-sdF):F2}"));
            d.Append(F($" L {X(BackZ(-sdB)):F2},{Y(-sdB):F2}"));
            for (int i = 0; i <= nPts; i++)
            {
                double y = sdB * (-1.0 + 2.0 * i / nPts);
                d.Append(F($" L {X(BackZ(y)):F2},{Y(y):F2}"));
            }
            d.Append(F($" L {X(BackZ(sdB)):F2},{Y(sdF):F2}"));
        }
        else
        {
            d.Append(F($" L {X(FrontZ(-sdF)):F2},{Y(-sdB):F2}"));
            d.Append(F($" L {X(BackZ(-sdB)):F2},{Y(-sdB):F2}"));
            for (int i = 0; i <= nPts; i++)
            {
                double y = sdB * (-1.0 + 2.0 * i / nPts);
                d.Append(F($" L {X(BackZ(y)):F2},{Y(y):F2}"));
            }
            d.Append(F($" L {X(FrontZ(sdF)):F2},{Y(sdB):F2}"));
        }
        return d.Append(" Z").ToString();
    }

    /// <summary>A surface's profile across its aperture.</summary>
    private static string Profile(Surface s, double z0, double sd, Func<double, double> X, Func<double, double> Y)
    {
        const int nPts = 51;
        var d = new StringBuilder();
        for (int i = 0; i <= nPts; i++)
        {
            double y = sd * (1.0 - 2.0 * i / nPts);
            d.Append(i == 0 ? F($"M {X(z0 + Sag(s, y, sd)):F2},{Y(y):F2}") : F($" L {X(z0 + Sag(s, y, sd)):F2},{Y(y):F2}"));
        }
        return d.ToString();
    }

    /// <summary>A surface's sag, held at its last real value beyond where a steep conic stops.</summary>
    private static double Sag(Surface s, double y, double sd)
    {
        double v = s.Sag(Math.Abs(y));
        if (double.IsFinite(v)) return v;
        for (double r = Math.Abs(y); r > 0; r -= sd / 200)
        {
            v = s.Sag(r);
            if (double.IsFinite(v)) return v;
        }
        return 0.0;
    }

    private static string Points(IEnumerable<(double Z, double Y)> line, Func<double, double> X, Func<double, double> Y) =>
        string.Join(" ", line.Select(p => F($"{X(p.Z):F2},{Y(p.Y):F2}")));

    /// <summary>What the ghost is, top left; what the colours are, top right.</summary>
    private static void Panels(StringBuilder sb, GhostResult r, Ghost g, double field, List<Ray> fan, int width,
                               bool sensorReflects, bool focusShown, string? imageNote)
    {
        var at = g.Fields.Where(f => Math.Abs(f.Field - field) < 1e-12).DefaultIfEmpty(g.Peak).FirstOrDefault();
        var lines = new List<string>
        {
            F($"Irradiance {(at?.Brightness ?? g.Irradiance):0.00E+00} (1 entering)"),
            F($"Power {g.Power:0.00E+00}   ΔZ {g.DeltaZ:0.###}"),
            $"Stop S{g.StopSurface}{(g.Anomalous ? " (not the lens's)" : "")}",
            F($"{fan.Count(f => f.Arrived)} of {fan.Count} rays drawn reach the sensor"),
        };
        if (!focusShown) lines.Add("Ghost focus off the drawing");
        if (imageNote != null) lines.Add(imageNote);
        Box(sb, 12, 34, imageNote != null ? 300 : 230, lines, null);

        var legend = new List<(string Text, string Color)>();
        int legs = fan.Count == 0 ? 0 : fan.Max(f => f.Legs.Count(l => l.Count > 1));
        int image = r.Lens.Surfaces.Count - 1;
        for (int leg = 0; leg < Math.Max(legs, g.Path.Reflections + 1); leg++)
        {
            string text = leg == 0 ? "in"
                : g.Path.Surfaces[leg - 1] == image ? "after the sensor"
                : "after S" + g.Path.Surfaces[leg - 1].ToString(C);
            if (leg > 0 && g.Path.Surfaces[leg - 1] == image && g.Orders.Count > 0)
            {
                int k = g.Path.Surfaces.Take(leg).Count(s => s == image) - 1;
                if (k >= 0 && k < g.Orders.Count) text += " " + Sensor.Label(g.Orders[k].M, g.Orders[k].N);
            }
            legend.Add((text, LegColors[leg % LegColors.Length]));
        }
        legend.Add(("the lens's own rays (dashed)", "#bbbbbb"));
        Box(sb, width - 12 - 190, 34, 190, legend.Select(l => l.Text).ToList(), legend.Select(l => l.Color).ToList());
    }

    private static void Box(StringBuilder sb, double x, double y, double w, List<string> lines, List<string>? swatches)
    {
        double rowH = 14, pad = 7;
        double h = pad * 2 + rowH * lines.Count;
        sb.AppendLine(F($"<rect x=\"{x:F1}\" y=\"{y:F1}\" width=\"{w:F1}\" height=\"{h:F1}\" fill=\"white\" fill-opacity=\"0.92\" stroke=\"#bbb\" stroke-width=\"0.5\" rx=\"3\"/>"));
        for (int i = 0; i < lines.Count; i++)
        {
            double ty = y + pad + (i + 1) * rowH - 3;
            double tx = x + pad;
            if (swatches != null)
            {
                sb.AppendLine(F($"<rect x=\"{tx:F1}\" y=\"{ty - 5:F1}\" width=\"14\" height=\"3\" fill=\"{swatches[i]}\"/>"));
                tx += 20;
            }
            sb.AppendLine(F($"<text x=\"{tx:F1}\" y=\"{ty:F1}\" font-size=\"10\" fill=\"#333\">{Esc(lines[i])}</text>"));
        }
    }

    /// <summary>A scale bar of a round length about a quarter of the drawing's width, and the lens's track.</summary>
    private static void ScaleBar(StringBuilder sb, double scale, int width, int height, int marginY, double track, double wavelength)
    {
        double targetMm = width * 0.25 / scale;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(targetMm)));
        double norm = targetMm / mag;
        double nice = norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10;
        double barMm = nice * mag, barPx = barMm * scale;
        double bx = (width - barPx) / 2.0, by = height - marginY + 32, tick = 6;
        sb.AppendLine(F($"<line x1=\"{bx:F1}\" y1=\"{by:F1}\" x2=\"{bx + barPx:F1}\" y2=\"{by:F1}\" stroke=\"black\" stroke-width=\"2\"/>"));
        sb.AppendLine(F($"<line x1=\"{bx:F1}\" y1=\"{by - tick:F1}\" x2=\"{bx:F1}\" y2=\"{by + tick:F1}\" stroke=\"black\" stroke-width=\"1.5\"/>"));
        sb.AppendLine(F($"<line x1=\"{bx + barPx:F1}\" y1=\"{by - tick:F1}\" x2=\"{bx + barPx:F1}\" y2=\"{by + tick:F1}\" stroke=\"black\" stroke-width=\"1.5\"/>"));
        sb.AppendLine(F($"<text x=\"{bx + barPx / 2:F1}\" y=\"{by + 16:F1}\" text-anchor=\"middle\" font-size=\"12\" font-weight=\"bold\" fill=\"#333\">{barMm:0.###} mm</text>"));
        sb.AppendLine(F($"<text x=\"{width / 2.0:F1}\" y=\"{by + 31:F1}\" text-anchor=\"middle\" font-size=\"11\" fill=\"#555\">Track {track:0.###} mm   λ = {wavelength:0.######} µm</text>"));
    }

    private static double Finite(double t) => double.IsFinite(t) ? t : 0.0;

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
