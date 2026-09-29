using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Enums;
using GhostAnalysis.Core.Ghosts;

namespace GhostAnalysis.Core.Reporting;

/// <summary>
/// The ghosts of a lens as text: the brightest at its worst field first, then the brightest
/// ones field by field, then each one's first order.
/// </summary>
public static class Report
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    /// <param name="detailed">How many of the brightest ghosts to show field by field.</param>
    public static string Write(GhostResult r, int detailed = 5)
    {
        var sb = new StringBuilder();
        var lens = r.Lens;
        int image = lens.Surfaces.Count - 1;
        string unit = lens.FieldType == FieldType.ObjectAngle ? "deg" : "(object height)";
        bool traced = r.Options.RealRays;

        sb.AppendLine($"Ghost analysis: {lens.Title}");
        sb.AppendLine();
        sb.AppendLine(F($"Wavelength:        {r.Wavelength:0.######} um"));
        sb.AppendLine(F($"Focal length:      {r.Nominal.Efl:0.######}"));
        sb.AppendLine(F($"Entrance pupil:    {r.Nominal.Epd:0.######}"));
        sb.AppendLine($"Reflections:       {r.Options.Reflections} per ghost");
        sb.AppendLine(r.Options.CoatedReflectance is double c
            ? F($"Surfaces:          coated, R = {c:0.####} at glass-air; cemented surfaces Fresnel")
            : "Surfaces:          uncoated, Fresnel reflectance at normal incidence");
        sb.AppendLine(r.Options.ImageReflects
            ? F($"Sensor:            surface {image}, R = {r.Options.ImageReflectance:0.####} (a setting: no lens file gives it)")
            : "Sensor:            not reflecting");
        var sensor = r.Options.Sensor;
        if (r.Options.ImageReflects || sensor.Bounded)
            sb.AppendLine(sensor.Bounded
                ? F($"Sensor size:       {(sensor.Width.HasValue ? $"{sensor.Width:0.###}" : "unbounded")} across x {(sensor.Height.HasValue ? $"{sensor.Height:0.###}" : "unbounded")} along the field, centred")
                : "Sensor size:       no edge (every ray reaching the image plane counts)");
        if (sensor.Diffracts)
        {
            var orders = sensor.Orders(r.Wavelength, 1.0);
            sb.AppendLine(F($"Sensor grating:    period {sensor.PeriodX ?? sensor.PeriodY:0.###} x {sensor.PeriodY ?? sensor.PeriodX:0.###} um, orders to ±{sensor.MaxOrder}, ") +
                          (sensor.Efficiencies != null ? "measured efficiencies" : F($"efficiencies sinc² with fill {sensor.FillFactor:0.###}")));
            sb.AppendLine("  Orders analysed, and the share of the sensor's reflected light in each:");
            foreach (var chunk in orders.Chunk(6))
                sb.AppendLine("    " + string.Join("  ", chunk.Select(o => F($"{Sensor.Label(o.M, o.N),-8} {o.Efficiency:0.0000}"))));
        }
        sb.AppendLine(F($"Power entering:    {r.Options.InputPower:G4}, the same at every field"));
        sb.AppendLine($"Fields:            {string.Join(", ", r.Fields.Select(f => f.ToString("0.###", C)))} {unit}");
        sb.AppendLine(traced
            ? $"Spots:             real rays, a {r.Options.PupilSamples | 1}-across grid over each ghost's entrance pupil"
            : "Spots:             paraxial only");
        if (r.Unresolved.Count > 0)
            sb.AppendLine($"WARNING: glasses the catalogs lack, traced as air: {string.Join(", ", r.Unresolved)}");
        sb.AppendLine();

        sb.AppendLine("Surface reflectances:");
        for (int k = 1; k <= image; k++)
            if (r.Reflectance[k] > 0) sb.AppendLine(F($"  {k,3}  {r.Reflectance[k]:0.000000}{(k == image ? "  (sensor)" : "")}"));
        sb.AppendLine();

        sb.AppendLine($"{r.Ghosts.Count} ghosts, brightest at their worst field first:");
        int wide = Math.Max(12, r.Ghosts.Count == 0 ? 12 : r.Ghosts.Max(g => g.Name.Length));
        sb.AppendLine($"  {"Ghost".PadRight(wide)}  Peak irrad.   at field     x on sensor y      Radius    Axis irrad.       ΔZ     Mag   Stop");
        foreach (var g in r.Ranked)
        {
            var p = g.Peak;
            double rad = p == null ? Math.Abs(g.MarginalAtImage) : p.Traced && p.Transmitted > 0 ? p.EffectiveRadius : p.ParaxialRadius;
            double x = p == null ? 0.0 : p.Traced && p.Transmitted > 0 ? p.CentroidX : p.ParaxialCenterX;
            sb.AppendLine(F($"  {g.Name.PadRight(wide)} {g.PeakIrradiance,12:0.000E+00} {p?.Field ?? 0,10:0.###} {x,9:0.0000} {p?.Position ?? 0,9:0.0000} {rad,11:0.0000} {g.Irradiance,14:0.000E+00} {g.DeltaZ,9:0.000} {g.Magnification,7:0.000}   {g.StopSurface}{(g.Anomalous ? " *" : "")}"));
        }
        sb.AppendLine("  Peak irradiance at the field where the ghost is brightest, landing at x, y on the sensor there");
        sb.AppendLine("  with that radius; Axis irrad. and ΔZ on axis, paraxially; Mag is where the ghost lands as a multiple");
        sb.AppendLine("  of the image height (-1: mirrored through the centre). * stopped by a surface other than the lens's");
        sb.AppendLine("  stop. A ghost reflecting from a diffracting sensor carries its order there, (m,n).");
        sb.AppendLine();

        // The ghosts that come into focus on the sensor somewhere off axis. A third-order prediction
        // far beyond the fields analysed says nothing: the theory does not hold out there.
        double reach = 2.0 * (r.Fields.Count == 0 ? 0.0 : r.Fields.Max(Math.Abs));
        double Near(double f) => double.IsFinite(f) && f <= reach ? f : double.NaN;
        var focusing = r.Ranked.Where(g => g.Crossings.Count > 0
                                        || double.IsFinite(Near(g.PredictedTangentialCrossing))
                                        || double.IsFinite(Near(g.PredictedSagittalCrossing))).ToList();
        sb.AppendLine("Ghosts that focus on the sensor off axis (tangential T, sagittal S):");
        if (focusing.Count == 0) sb.AppendLine("  none");
        else
        {
            sb.AppendLine("  Ghost          Real crossings                         Third-order prediction");
            foreach (var g in focusing)
            {
                string real = g.Crossings.Count == 0
                    ? (traced ? "none within the fields" : "(not traced)")
                    : string.Join(", ", g.Crossings.Select(x =>
                    {
                        var at = g.Fields.FirstOrDefault(f => f.Marker == x.Kind && f.Field == x.Field);
                        return F($"{x.Kind} {x.Field:0.###}") + (at != null && at.Transmitted <= 0 ? " (vignetted)" : "");
                    }));
                string predicted = string.Join(", ", new[]
                {
                    double.IsFinite(Near(g.PredictedTangentialCrossing)) ? F($"T {g.PredictedTangentialCrossing:0.###}") : null,
                    double.IsFinite(Near(g.PredictedSagittalCrossing)) ? F($"S {g.PredictedSagittalCrossing:0.###}") : null,
                }.Where(x => x != null));
                sb.AppendLine($"  {g.Name.PadRight(wide)}   {real,-38} {(predicted.Length == 0 ? "none" : predicted)}");
            }
            sb.AppendLine($"  Fields in {unit}. Real: where the real foci either side of the ghost's chief ray reach the");
            sb.AppendLine("  sensor; (vignetted) where none of the ghost's light gets there. Third order: where the");
            sb.AppendLine("  ghost's image surfaces, from its Seidel sums, meet it, if within twice the fields analysed.");
        }
        sb.AppendLine();

        foreach (var g in r.Ranked.Take(detailed))
        {
            sb.AppendLine(F($"{g.Name}: field stop {(g.FieldStopSurface < 0 ? "none" : $"surface {g.FieldStopSurface}")}, unvignetted to {g.UnvignettedField:0.###} {unit}"));
            if (traced)
            {
                sb.AppendLine("      Field      Image   Ghost par.  Centroid      RMS      Max   Passed    Irradiance   T focus   S focus");
                foreach (var f in g.Fields)
                    sb.AppendLine(F($"  {f.Marker ?? " "}{f.Field,8:0.###} {f.ImageHeight,10:0.0000} {f.ParaxialCenter,10:0.0000} {f.CentroidY,9:0.0000} {f.RmsRadius,8:0.0000} {f.MaxRadius,8:0.0000} {f.Transmitted,8:0.000} {f.Irradiance,13:0.000E+00} {f.TangentialFocus,9:0.000} {f.SagittalFocus,9:0.000}"));
            }
            else
            {
                sb.AppendLine("     Field      Image      Ghost   Radius   Passed    Irradiance   T focus   S focus");
                foreach (var f in g.Fields)
                    sb.AppendLine(F($"  {f.Field,8:0.###} {f.ImageHeight,10:0.0000} {f.ParaxialCenter,10:0.0000} {f.ParaxialRadius,8:0.0000} {f.ParaxialTransmitted,8:0.000} {f.ParaxialIrradiance,13:0.000E+00} {f.PredictedTangentialFocus,9:0.000} {f.PredictedSagittalFocus,9:0.000}"));
            }
            sb.AppendLine();
        }
        if (r.Ghosts.Count > 0)
        {
            if (traced)
            {
                sb.AppendLine("  Image: where the lens images the field. Ghost par.: the ghost's paraxial centre. Centroid,");
                sb.AppendLine("  RMS and Max: its real spot. Passed: the share of rays not vignetted. Irradiance: its power");
                sb.AppendLine("  over a disc of radius √2 × RMS (the radius of an even disc of that RMS), but no smaller than");
                sb.AppendLine("  the Airy radius of its cone. T and S focus: how far short of the sensor its real tangential");
                sb.AppendLine("  and sagittal foci fall (0: in focus on it). A field marked T or S is a crossing; one");
                sb.AppendLine("  marked P is where a fine scan of the real spot over field found the ghost brightest.");
            }
            else
            {
                sb.AppendLine("  T and S focus: how far short of the sensor the ghost's tangential and sagittal foci fall,");
                sb.AppendLine("  by its third-order image surfaces (0: in focus on it).");
            }
            sb.AppendLine();
        }

        // An order shares its ghost's first order; each ghost is listed once.
        sb.AppendLine("First order of each ghost:");
        sb.AppendLine("  Ghost             f_E        BFD         d'     y'_g,n       D_ep      L'_g    L'_g,n      D_xp       f/#");
        foreach (var g in r.Ghosts.Where(x => x.Orders.All(o => o == (0, 0))).GroupBy(x => x.Path.ToString()).Select(x => x.First()))
            sb.AppendLine(F($"  {g.Path,-12} {g.Efl,10:0.0000} {g.Bfd,10:0.0000} {g.RearPrincipalPlane,10:0.0000} {g.MarginalAtImage,10:0.0000} {g.EntrancePupilDiameter,10:0.0000} {g.ExitPupilToGhostImage,9:0.0000} {g.ExitPupilToImage,9:0.0000} {g.ExitPupilDiameter,9:0.0000} {g.FNumber,9:0.0000}"));
        sb.AppendLine();
        sb.AppendLine("  BFD and d' from the last surface; ΔZ from the ghost's image to the lens's, positive when the");
        sb.AppendLine("  ghost focuses short of it; L' from the ghost's exit pupil to the ghost's image (L'_g) and to the");
        sb.AppendLine("  lens's image (L'_g,n).");
        return sb.ToString();
    }

    private static string F(FormattableString s) => s.ToString(C);
}
