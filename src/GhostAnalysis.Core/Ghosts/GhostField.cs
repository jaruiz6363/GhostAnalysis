namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// One ghost at one field: where it lands on the sensor, how large it is, how much of it gets
/// through, and how bright it is.
///
/// <para>Heights are on the sensor, in lens units, in the meridional plane (y) and across it
/// (x); the field is in the lens's own field units - degrees, or object height. The paraxial
/// columns are the papers' (2009 eqs. 10-11, 2011 eqs. 17-18); the real ones come from tracing
/// a grid of real rays across the ghost's entrance pupil through the unfolded ghost.</para>
/// </summary>
public sealed class GhostField
{
    /// <summary>The field, in the lens's field units.</summary>
    public required double Field { get; init; }

    /// <summary>Where the lens images this field: its paraxial chief ray at the sensor.</summary>
    public required double ImageHeight { get; init; }

    // ── Paraxial ─────────────────────────────────────────────────────────────────

    /// <summary>Centre of the ghost's disc on the sensor: the ghost chief ray's height there, ȳ'_g,n.</summary>
    public required double ParaxialCenter { get; init; }

    /// <summary>
    /// Centre of the ghost's disc across the field's plane: zero, but for a diffraction order with
    /// m ≠ 0, which the sensor's grating throws sideways.
    /// </summary>
    public double ParaxialCenterX { get; init; }

    /// <summary>Radius of the ghost's disc on the sensor, |y'_g,n|. The same at every field.</summary>
    public required double ParaxialRadius { get; init; }

    /// <summary>
    /// Fraction of the ghost's beam not vignetted, paraxially: at each surface the beam is a
    /// disc of the marginal ray's radius about the chief ray, and the fraction is the least
    /// share of it any surface's aperture passes.
    /// </summary>
    public required double ParaxialTransmitted { get; init; }

    /// <summary>The ghost's power at this field, over its paraxial disc, per unit area.</summary>
    public required double ParaxialIrradiance { get; init; }

    // ── Real rays ────────────────────────────────────────────────────────────────

    /// <summary>Whether real rays were traced. False when the analysis was paraxial only.</summary>
    public required bool Traced { get; init; }

    /// <summary>
    /// Fraction of the rays launched that reached the sensor, none vignetted, lost, or landing -
    /// or, for a sensor ghost, reflecting - off the sensor's edges.
    /// </summary>
    public double Transmitted { get; init; }

    /// <summary>Centroid of the ghost's real spot on the sensor.</summary>
    public double CentroidY { get; init; }
    public double CentroidX { get; init; }

    /// <summary>The real ghost chief ray's height at the sensor, NaN if it did not arrive.</summary>
    public double ChiefY { get; init; } = double.NaN;

    /// <summary>RMS radius of the real spot about its centroid.</summary>
    public double RmsRadius { get; init; }

    /// <summary>Largest distance of a ray from the centroid.</summary>
    public double MaxRadius { get; init; }

    /// <summary>
    /// The radius the ghost's power is taken to spread over: √2 × RMS, which for an evenly
    /// filled disc is its radius, but no smaller than the Airy radius 1.22 λ N of the ghost's
    /// cone, below which diffraction, not geometry, sets the size.
    /// </summary>
    public double EffectiveRadius { get; init; }

    /// <summary>The ghost's power at this field over its effective disc, per unit area.</summary>
    public double Irradiance { get; init; }

    // ── Focus ────────────────────────────────────────────────────────────────────
    // Distances from the sensor to where the ghost's light comes to a focus, positive when it
    // focuses short of the sensor, as ΔZ is. Tangential: the rays in the plane of the field;
    // sagittal: across it. Zero is a ghost in focus on the sensor.

    /// <summary>
    /// The tangential focus by third-order theory: ΔZ plus the ghost's tangential field
    /// curvature, (3 S_III + S_IV) / (2 n' u'^2) at this field (2011, eqs. 10 and 23).
    /// </summary>
    public double PredictedTangentialFocus { get; init; } = double.NaN;

    /// <summary>The sagittal focus by third-order theory, with (S_III + S_IV) / (2 n' u'^2).</summary>
    public double PredictedSagittalFocus { get; init; } = double.NaN;

    /// <summary>The real tangential focus: where two real rays either side of the chief ray, in the field's plane, cross.</summary>
    public double TangentialFocus { get; init; } = double.NaN;

    /// <summary>The real sagittal focus, from two rays either side of the chief ray across the field's plane.</summary>
    public double SagittalFocus { get; init; } = double.NaN;

    /// <summary>
    /// Why this field was added to the sweep: "T" or "S" where the ghost's real tangential or sagittal
    /// focus crosses the sensor, "P" where a fine scan of the real spot found the ghost brightest.
    /// </summary>
    public string? Marker { get; init; }

    /// <summary>The irradiance this field is ranked by: the real one when traced, otherwise the paraxial.</summary>
    public double Brightness => Traced ? Irradiance : ParaxialIrradiance;

    /// <summary>The ghost's position this field is reported at: the real centroid when traced.</summary>
    public double Position => Traced && Transmitted > 0 ? CentroidY : ParaxialCenter;
}
