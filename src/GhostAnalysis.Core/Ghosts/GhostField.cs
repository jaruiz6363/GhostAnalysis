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

    /// <summary>Fraction of the rays launched that reached the sensor, none vignetted or lost.</summary>
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

    /// <summary>The irradiance this field is ranked by: the real one when traced, otherwise the paraxial.</summary>
    public double Brightness => Traced ? Irradiance : ParaxialIrradiance;

    /// <summary>The ghost's position this field is reported at: the real centroid when traced.</summary>
    public double Position => Traced && Transmitted > 0 ? CentroidY : ParaxialCenter;
}
