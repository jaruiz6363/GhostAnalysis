using AberrationCalculator.Core.Models;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// A ghost unfolded into a lens of its own.
///
/// <para>The light goes forward to the first reflecting surface, back through the lens to the
/// second, forward again, and so on to the image: Abd El-Maksoud and Sasian's sub-layouts TF1,
/// TB1, TF2, ... Each is the lens's own prescription, run backwards where the light runs
/// backwards. Written as a sequential lens, that is a FOLDED lens - the reflecting surfaces
/// are mirrors, the thicknesses after them negative until the next - and AberrationCalculator
/// traces and analyses a folded lens as it does any other. So the ghost gets everything the
/// lens gets: its first order, its pupils, and its aberrations.</para>
///
/// <para>Where the light runs backwards, the medium after a surface is the one before it in the
/// lens: surface i crossed backwards leaves the light in the glass (or air) between i-1 and i.
/// A mirror stays in the medium the light arrived in, which is how AberrationCalculator
/// reads one.</para>
/// </summary>
public sealed class GhostLayout
{
    public GhostPath Path { get; }

    /// <summary>The unfolded ghost, object to image.</summary>
    public OpticalSystem System { get; }

    /// <summary>For each surface of <see cref="System"/>, the lens surface it is (0 the object, M+1 the image).</summary>
    public IReadOnlyList<int> Origin { get; }

    /// <summary>Whether each surface of <see cref="System"/> is one of the ghost's reflections.</summary>
    public IReadOnlyList<bool> Reflects { get; }

    private GhostLayout(GhostPath path, OpticalSystem system, int[] origin, bool[] reflects)
    {
        Path = path;
        System = system;
        Origin = origin;
        Reflects = reflects;
    }

    /// <summary>Unfolds <paramref name="path"/> in <paramref name="lens"/>.</summary>
    public static GhostLayout Build(OpticalSystem lens, GhostPath path)
    {
        if (lens == null) throw new ArgumentNullException(nameof(lens));
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (lens.Surfaces.Any(s => s.IsMirror))
            throw new NotSupportedException("The lens has a mirror in it; ghosts of a catadioptric lens are not analysed yet.");
        int image = lens.Surfaces.Count - 1;
        if (path.Surfaces.Any(k => k > image))
            throw new ArgumentException($"{path} names a surface the lens does not have: its image is surface {image}.", nameof(path));

        // Where each lens surface sits along the axis, surface 1 at zero.
        var z = new double[lens.Surfaces.Count];
        for (int i = 2; i <= image; i++) z[i] = z[i - 1] + lens.Surfaces[i - 1].Thickness;

        // The surfaces the light meets, in order, and which way it is going through each.
        var visits = new List<(int Surface, bool Forward, bool Reflect)>();
        int at = 0;
        bool forward = true;
        foreach (int k in path.Surfaces)
        {
            if (forward) for (int i = at + 1; i < k; i++) visits.Add((i, true, false));
            else for (int i = at - 1; i > k; i--) visits.Add((i, false, false));
            visits.Add((k, forward, true));
            forward = !forward;
            at = k;
        }
        for (int i = at + 1; i < image; i++) visits.Add((i, true, false));

        var ghost = new OpticalSystem
        {
            Title = $"{path} of {lens.Title}".Trim(),
            Designer = lens.Designer,
            Aperture = new Aperture(lens.Aperture.Type, lens.Aperture.Value),
            FieldType = lens.FieldType,
            GlassCatalogs = new List<string>(lens.GlassCatalogs),
            GlassCatalogsAreInferred = lens.GlassCatalogsAreInferred,
            IsAfocal = lens.IsAfocal,
            TelecentricObjectSpace = lens.TelecentricObjectSpace,
        };
        foreach (var w in lens.Wavelengths) ghost.Wavelengths.Add(new Wavelength(w.Value, w.Weight, w.IsPrimary));
        foreach (var f in lens.Fields) ghost.Fields.Add(new Field(f.Y, f.Weight) { X = f.X });

        var origin = new List<int> { 0 };
        var reflects = new List<bool> { false };
        var obj = Copy(lens.Surfaces[0], lens.Surfaces[0]);
        obj.Thickness = lens.Surfaces[0].Thickness;
        ghost.Surfaces.Add(obj);

        for (int v = 0; v < visits.Count; v++)
        {
            var (i, fwd, reflect) = visits[v];
            // The medium the light is in after this surface: after a mirror the one it arrived
            // in; after a surface crossed backwards the one before that surface in the lens.
            var medium = reflect ? null : fwd ? lens.Surfaces[i] : lens.Surfaces[i - 1];
            var s = Copy(lens.Surfaces[i], medium);
            if (reflect) s.Material = "MIRROR";
            int next = v + 1 < visits.Count ? visits[v + 1].Surface : image;
            s.Thickness = z[next] - z[i];
            ghost.Surfaces.Add(s);
            origin.Add(i);
            reflects.Add(reflect);
        }

        var img = Copy(lens.Surfaces[image], lens.Surfaces[image]);
        img.Thickness = 0.0;
        ghost.Surfaces.Add(img);
        origin.Add(image);
        reflects.Add(false);

        for (int i = 0; i < ghost.Surfaces.Count; i++) ghost.Surfaces[i].Index = i;
        return new GhostLayout(path, ghost, origin.ToArray(), reflects.ToArray());
    }

    /// <summary>
    /// The shape of <paramref name="shape"/> - curvature, figuring, aperture - with the medium
    /// after it taken from <paramref name="medium"/> (air when null). Not a stop: the ghost's
    /// stop is found, not inherited.
    /// </summary>
    private static Surface Copy(Surface shape, Surface? medium)
    {
        var s = new Surface
        {
            Type = shape.Type,
            Curvature = shape.Curvature,
            Conic = shape.Conic,
            AsphericCoefficients = (double[])shape.AsphericCoefficients.Clone(),
            SemiDiameter = shape.SemiDiameter,
            SemiDiameterMode = shape.SemiDiameterMode,
            ClearAperturePercent = shape.ClearAperturePercent,
            ObscurationRadius = shape.ObscurationRadius,
            FocalLength = shape.FocalLength,
            Comment = shape.Comment,
            IsStop = false,
        };
        if (medium != null)
        {
            s.Material = medium.Material;
            s.CatalogName = medium.CatalogName;
            s.ModelIndexEnabled = medium.ModelIndexEnabled;
            s.ModelNd = medium.ModelNd;
            s.ModelVd = medium.ModelVd;
            s.ModelDPgF = medium.ModelDPgF;
        }
        return s;
    }
}
