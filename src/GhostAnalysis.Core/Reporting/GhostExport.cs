using System.Globalization;
using System.Text;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using GhostAnalysis.Core.Ghosts;

namespace GhostAnalysis.Core.Reporting;

/// <summary>
/// The ghosts written out for other programs: each ghost's unfolded lens as a lens file, and every
/// ghost's results as a table.
///
/// <para>An unfolded ghost is an ordinary folded sequential lens - mirrors at its reflections,
/// negative thicknesses between them - so any lens program that reads the format can analyse it
/// independently: its first order, its real rays, its spots. That is how GhostAnalysis is checked
/// against LensHH-LT (docs/verification.md), and how a user can check any ghost they doubt.</para>
/// </summary>
public static class GhostExport
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    /// <summary>
    /// Writes one ghost's unfolded lens to <paramref name="path"/>, in the format its extension names:
    /// the ghost's own stop and entrance pupil, the apertures the analysis stopped its rays at, the
    /// fields analysed and the given wavelengths. A diffracted order cannot be written - no lens
    /// format carries the sensor's grating - and is written as its path's zeroth order.
    /// </summary>
    /// <returns>The lens writer's notes, if any.</returns>
    public static IReadOnlyList<string> WriteLayout(GhostResult r, Ghost g, string path, GlassCatalog catalog,
                                                    IReadOnlyList<(double Um, double Weight)>? wavelengths = null,
                                                    int primary = 0)
    {
        var sys = g.Layout.System;
        int image = r.Lens.Surfaces.Count - 1;
        int last = sys.Surfaces.Count - 1;

        // A surface the analysis does not clip - the sensor, reflecting; one the file does not size -
        // must not be left without a size, or the reading program sizes it itself, from the lens's
        // own beam, and clips the ghost there: LensHH-LT made a reflecting sensor 1.7 mm across and
        // stopped half a ghost at it. It is given one just past everything the ghost's rays reach.
        var reach = new double[sys.Surfaces.Count];
        foreach (double h in r.Fields.Where(f => f >= 0).Distinct())
            for (int iy = 0; iy <= 20; iy++)
                for (int ix = 0; ix <= 20; ix++)
                {
                    double py = -1 + iy / 10.0, px = -1 + ix / 10.0;
                    if (px * px + py * py > 1 + 1e-12) continue;
                    g.Tracer.Trace(h, py, px, clip: false, (j, hit) =>
                        reach[j] = Math.Max(reach[j], Math.Sqrt(hit.X * hit.X + hit.Y * hit.Y)));
                }
        var copy = new OpticalSystem
        {
            Title = sys.Title,
            Designer = sys.Designer,
            // A finite object is given its beam by object-space NA, which lens programs take only with
            // object-height fields; with angles, the same beam is written as its entrance pupil.
            Aperture = sys.Aperture.Type == AberrationCalculator.Core.Enums.ApertureType.ObjectSpaceNA
                       && sys.FieldType == AberrationCalculator.Core.Enums.FieldType.ObjectAngle
                ? new Aperture(AberrationCalculator.Core.Enums.ApertureType.EPD, g.EntrancePupilDiameter)
                : new Aperture(sys.Aperture.Type, sys.Aperture.Value),
            FieldType = sys.FieldType,
            // Traced as the analysis traced it: aimed at the stop, or across the paraxial pupil.
            RayAiming = g.Tracer.Aims ? AberrationCalculator.Core.Enums.RayAimingMode.Real
                                      : AberrationCalculator.Core.Enums.RayAimingMode.Off,
            GlassCatalogs = new List<string>(sys.GlassCatalogs),
            GlassCatalogsAreInferred = sys.GlassCatalogsAreInferred,
        };
        var waves = wavelengths ?? new[] { (r.Wavelength, 1.0) };
        for (int i = 0; i < waves.Count; i++)
            copy.Wavelengths.Add(new Wavelength(waves[i].Um, waves[i].Weight, i == primary));
        foreach (double f in r.Fields.Where(f => f >= 0).Distinct().OrderBy(f => f))
            copy.Fields.Add(new Field(f));

        for (int j = 0; j < sys.Surfaces.Count; j++)
        {
            var s = sys.Surfaces[j];
            int origin = g.Layout.Origin[j];
            var t = new Surface
            {
                Index = j,
                Type = s.Type,
                Curvature = s.Curvature,
                Thickness = s.Thickness,
                Conic = s.Conic,
                AsphericCoefficients = (double[])s.AsphericCoefficients.Clone(),
                Material = s.Material,
                CatalogName = s.CatalogName,
                ModelIndexEnabled = s.ModelIndexEnabled,
                ModelNd = s.ModelNd,
                ModelVd = s.ModelVd,
                ModelDPgF = s.ModelDPgF,
                IsStop = j == g.StopLayoutSurface,
                FocalLength = s.FocalLength,
                Comment = j == 0 ? s.Comment : $"lens surface {origin}{(g.Layout.Reflects[j] ? ", reflecting" : "")}",
                // The apertures the analysis stopped the ghost's rays at: the lens's, or where it gave
                // none those sized to its beam. Where it stops nothing, past everything that arrives.
                SemiDiameter = j == 0 || j == last ? 0.0
                             : origin != image && r.Apertures[origin] > 0 ? r.Apertures[origin]
                             : Math.Max(reach[j] * 1.01, 1e-3),
            };
            if (j > 0 && j < last) t.SemiDiameterMode = AberrationCalculator.Core.Enums.SemiDiameterMode.Fixed;
            copy.Surfaces.Add(t);
        }
        return LensFile.Write(copy, path, catalog, installOptilandGlasses: false);
    }

    /// <summary>
    /// Every ghost's results, a row per ghost, field and wavelength: its first order, where it lands,
    /// how large it is and how bright, as GhostAnalysis computes them. Comma separated, invariant
    /// culture, a header row first.
    /// </summary>
    public static string Csv(IEnumerable<GhostResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("wavelength_um,ghost,efl,bfd,delta_z,exit_pupil_from_last,exit_pupil_diameter,entrance_pupil_diameter," +
                      "stop_surface,field,marker,image_height,paraxial_center_y,chief_y,centroid_x,centroid_y,rms_radius," +
                      "max_radius,transmitted,irradiance,tangential_focus,sagittal_focus");
        foreach (var r in results)
            foreach (var g in r.Ghosts)
                foreach (var f in g.Fields)
                    sb.AppendLine(string.Join(",",
                        N(r.Wavelength), $"\"{g.Name}\"", N(g.Efl), N(g.Bfd), N(g.DeltaZ), N(g.Paraxial.ExitPupilFromLastSurface),
                        N(g.ExitPupilDiameter), N(g.EntrancePupilDiameter), g.StopSurface.ToString(C), N(f.Field), f.Marker ?? "",
                        N(f.ImageHeight), N(f.ParaxialCenter), N(f.ChiefY), N(f.CentroidX), N(f.CentroidY), N(f.RmsRadius),
                        N(f.MaxRadius), N(f.Transmitted), N(f.Brightness), N(f.TangentialFocus), N(f.SagittalFocus)));
        return sb.ToString();
    }

    private static string N(double v) => double.IsNaN(v) ? "" : v.ToString("R", C);
}
