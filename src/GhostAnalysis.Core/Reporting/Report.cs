using System.Globalization;
using System.Text;
using GhostAnalysis.Core.Ghosts;

namespace GhostAnalysis.Core.Reporting;

/// <summary>The ghosts of a lens as text: the brightest first, then each one's first order.</summary>
public static class Report
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static string Write(GhostResult r)
    {
        var sb = new StringBuilder();
        var lens = r.Lens;
        int image = lens.Surfaces.Count - 1;
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
        sb.AppendLine(F($"Power entering:    {r.Options.InputPower:G4}"));
        if (r.Unresolved.Count > 0)
            sb.AppendLine($"WARNING: glasses the catalogs lack, traced as air: {string.Join(", ", r.Unresolved)}");
        sb.AppendLine();

        sb.AppendLine("Surface reflectances:");
        for (int k = 1; k <= image; k++)
            if (r.Reflectance[k] > 0) sb.AppendLine(F($"  {k,3}  {r.Reflectance[k]:0.000000}{(k == image ? "  (sensor)" : "")}"));
        sb.AppendLine();

        sb.AppendLine($"{r.Ghosts.Count} ghosts, brightest at the image first:");
        sb.AppendLine("  Ghost          Irradiance       Power   Radius at image       ΔZ   Stop");
        foreach (var g in r.Ranked)
            sb.AppendLine(F($"  {g.Name,-12} {g.Irradiance,12:0.000E+00} {g.Power,11:0.000E+00} {Math.Abs(g.MarginalAtImage),17:0.0000} {g.DeltaZ,10:0.0000}   {g.StopSurface}{(g.Anomalous ? " *" : "")}"));
        if (r.Ghosts.Any(g => g.Anomalous))
            sb.AppendLine("  * stopped by a surface other than the lens's own stop");
        sb.AppendLine();

        sb.AppendLine("First order of each ghost (Abd El-Maksoud and Sasian's quantities):");
        sb.AppendLine("  Ghost             f_E        BFD         d'     y'_g,n       D_ep      L'_g    L'_g,n      D_xp       f/#");
        foreach (var g in r.Ghosts)
            sb.AppendLine(F($"  {g.Name,-12} {g.Efl,10:0.0000} {g.Bfd,10:0.0000} {g.RearPrincipalPlane,10:0.0000} {g.MarginalAtImage,10:0.0000} {g.EntrancePupilDiameter,10:0.0000} {g.ExitPupilToGhostImage,9:0.0000} {g.ExitPupilToImage,9:0.0000} {g.ExitPupilDiameter,9:0.0000} {g.FNumber,9:0.0000}"));
        sb.AppendLine();
        sb.AppendLine("  BFD and d' from the last surface; ΔZ from the ghost's image to the lens's, positive when the");
        sb.AppendLine("  ghost focuses short of it; L' from the ghost's exit pupil to the ghost's image (L'_g) and to the");
        sb.AppendLine("  lens's image (L'_g,n). Irradiance is the ghost's power spread evenly over its disc at the image.");
        return sb.ToString();
    }

    private static string F(FormattableString s) => s.ToString(C);
}
