using AberrationCalculator.Core.RayTrace;

namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// One ghost, analysed: its layout, its stop, its first order and its light at the image.
///
/// <para>The quantities are Abd El-Maksoud and Sasian's (SPIE 7428, 2009, Tables 5 and 6;
/// Appl. Opt. 50, 2305, 2011). Distances are in lens units and signed along the axis, positive
/// towards the image.</para>
/// </summary>
public sealed class Ghost
{
    public required GhostLayout Layout { get; init; }
    public GhostPath Path => Layout.Path;
    public string Name => Path.ToString();

    /// <summary>The paraxial trace of the ghost's layout, with its own stop and aperture.</summary>
    public required ParaxialResult Paraxial { get; init; }

    // ── Its stop ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// For each layout surface, how far an axial ray fills it: the ray's height over the
    /// surface's semi-diameter, for a ray of unit height at surface 1 (unit slope from a
    /// finite object). NaN where the surface has no aperture. The largest is the ghost's stop.
    /// </summary>
    public required IReadOnlyList<double> StopRatios { get; init; }

    /// <summary>Which surface of the layout is the ghost's aperture stop.</summary>
    public required int StopLayoutSurface { get; init; }

    /// <summary>Which surface of the lens that is.</summary>
    public int StopSurface => Layout.Origin[StopLayoutSurface];

    /// <summary>Whether the ghost is stopped by a surface other than the lens's own stop.</summary>
    public required bool Anomalous { get; init; }

    // ── First order ──────────────────────────────────────────────────────────────

    /// <summary>Ghost effective focal length, f_E,g.</summary>
    public double Efl => Paraxial.Efl;

    /// <summary>Ghost back focal distance BFD_g, from the lens's last surface.</summary>
    public double Bfd => Paraxial.Bfl;

    /// <summary>Ghost rear principal plane from the lens's last surface, d'_g = BFD_g - f'_R,g.</summary>
    public required double RearPrincipalPlane { get; init; }

    /// <summary>Where the ghost's image of the object forms, from the lens's last surface.</summary>
    public double ImageDistance => Paraxial.ParaxialFocusDistance;

    /// <summary>Ghost image plane to the lens's image plane, ΔZ_g,n: positive when the ghost focuses short of it.</summary>
    public required double DeltaZ { get; init; }

    /// <summary>Ghost marginal-ray height at the lens's image plane, y'_g,n: the radius of the ghost there.</summary>
    public double MarginalAtImage => Paraxial.Y[^1];

    /// <summary>Ghost entrance pupil diameter, D_ep,g.</summary>
    public double EntrancePupilDiameter => Paraxial.Epd;

    /// <summary>Ghost exit pupil diameter, D_xp,g.</summary>
    public double ExitPupilDiameter => Paraxial.ExitPupilDiameter;

    /// <summary>Ghost exit pupil to the ghost image, L'_g.</summary>
    public required double ExitPupilToGhostImage { get; init; }

    /// <summary>Ghost exit pupil to the lens's image plane, L'_g,n.</summary>
    public required double ExitPupilToImage { get; init; }

    /// <summary>Ghost F/number, f_E,g / D_ep,g.</summary>
    public double FNumber => Paraxial.FNumber;

    // ── Light ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The fraction of the light entering the ghost's pupil that leaves it: the product of a
    /// Fresnel transmittance at every surface crossed and a reflectance at every reflection,
    /// T_g (2011, eq. 16).
    /// </summary>
    public required double Transmittance { get; init; }

    /// <summary>Power the ghost carries to the image, for the power given as entering the lens.</summary>
    public required double Power { get; init; }

    /// <summary>
    /// The ghost's irradiance at the lens's image plane on axis: its power over its area there,
    /// spread evenly (2011, eq. 17). Infinite for a ghost focused exactly on the image.
    /// </summary>
    public required double Irradiance { get; init; }

    // ── Off axis ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Where the ghost lands for a field, as a multiple of where the lens images it:
    /// ȳ'_g,n / ȳ'_n. Paraxially the same at every field. Near 1 the ghost sits on the image;
    /// near -1 it is mirrored through the centre of the sensor. NaN when it cannot be traced.
    /// </summary>
    public required double Magnification { get; init; }

    /// <summary>
    /// The layout surface that first cuts into the ghost's beam off axis, its field stop (2009,
    /// section 6): largest |ȳ| / (a - |y|). -1 if no surface with an aperture does.
    /// </summary>
    public required int FieldStopLayoutSurface { get; init; }

    /// <summary>The lens surface that is.</summary>
    public int FieldStopSurface => FieldStopLayoutSurface < 0 ? -1 : Layout.Origin[FieldStopLayoutSurface];

    /// <summary>The largest field the ghost passes unvignetted, paraxially; infinite if nothing cuts it.</summary>
    public required double UnvignettedField { get; init; }

    /// <summary>
    /// The fields at which third-order theory puts the ghost's tangential and sagittal image
    /// surfaces on the sensor (2011, eqs. 24 and 25), where it is in focus off axis though it may
    /// be far out of focus on it. NaN where the surface curves away from the sensor.
    /// </summary>
    public required double PredictedTangentialCrossing { get; init; }
    public required double PredictedSagittalCrossing { get; init; }

    /// <summary>
    /// The fields, within those analysed, at which the ghost's real tangential or sagittal focus
    /// is on the sensor, from the axis out.
    /// </summary>
    public required IReadOnlyList<(double Field, string Kind)> Crossings { get; init; }

    /// <summary>The ghost at each field analysed, from the axis out, with its crossings among them.</summary>
    public required IReadOnlyList<GhostField> Fields { get; init; }

    /// <summary>The field at which the ghost is brightest, and so worst.</summary>
    public GhostField? Peak => Fields.Count == 0 ? null : Fields.MaxBy(f => f.Brightness);

    /// <summary>The ghost's irradiance at its brightest field.</summary>
    public double PeakIrradiance => Peak?.Brightness ?? Irradiance;
}
