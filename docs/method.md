# How GhostAnalysis works

This is the method, step by step: what is computed, how, and where it comes from. The
[user guide](user-guide.md) says how to run it; the [examples](examples.md) show it at work; the
[references](references.md) list the sources cited here as [1], [2], ...

The foundation is Abd El-Maksoud and Sasian's paraxial ghost analysis [1, 2]. GhostAnalysis
follows it for the ghosts' enumeration, layouts, stops, first order and light, and reproduces its
worked example to every printed digit (see *Validation* below). It then goes beyond it where a
lens designer's questions do: real rays, off-axis focusing, the sensor's size and its
diffraction, several wavelengths, and drawings.

All the optics underneath - the lens model, the file readers, the glass catalogs, the paraxial
and real-ray traces, the Seidel sums - are [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator)'s,
carried in `external/AberrationCalculator` as a `git subtree`.

Units are the lens's (millimetres); wavelengths and grating periods are in micrometres;
reflectances and efficiencies are fractions (0.1 is 10 %).

---

## 1. Which ghosts there are

A lens of M surfaces, the object at surface 0 and the image at M + 1. A ghost that reaches the
image reflects an even number of times: the first reflection turns the light back, the second
forward again, and so on. A two-reflection ghost **Gk1,k2** reflects from surface k1, travels
back to surface k2 < k1, reflects forward again, and goes on to the image. With N reflections
the surfaces alternate, k1 > k2 < k3 > k4 ..., enumerated by nested loops ([2], eq. 3):

    k1 = 2..top,   k2 = 1..k1-1,   k3 = k2+1..top,   k4 = 1..k3-1,  ...

**The sensor reflects.** The papers stop at surface M: `top = M`. A real sensor reflects several
per cent of what reaches it, often tens of per cent, and sensor ghosts are often the brightest
there are. So GhostAnalysis lets the image reflect too, `top = M + 1`, with a reflectance you give
(`--sensor-reflectance`, default 0.05). Two-reflection ghosts then number (M + 1)M/2 rather than
M(M − 1)/2. `--no-sensor` restores the papers' set.

A surface with no index step - a dummy, a stop in air - reflects nothing, and its ghosts are left
out.

## 2. Each ghost unfolded into a lens of its own

The light's path is the lens's own prescription run forwards, backwards, forwards again - the
papers' sub-layouts TF1, TB1, TF2, ... [1, 2]. Written as a sequential lens that is a **folded
lens**: the reflecting surfaces are mirrors, and between them the thicknesses are negative, the
convention every sequential program uses for a reflection. Crossing surface i backwards leaves the
light in the medium *before* i in the lens; a mirror stays in the medium the light arrived in.

That one step is the heart of the method. A folded lens is an ordinary lens to AberrationCalculator:
its paraxial trace, Seidel sums and real-ray trace all handle mirrors. So every ghost gets the
same analysis the lens gets, from the same code, with nothing specific to ghosts in the optics.

## 3. The ghost's aperture stop

A ghost need not be stopped by the lens's stop: light bouncing between two surfaces can be cut
off by a third. It is found as the 2009 paper finds it ([1], section 6): an axial ray - unit height
at surface 1 for an object at infinity, unit slope for a finite one - is traced through the
unfolded ghost, and at each surface j the ratio

    r_j = |y_j| / a_j          (a_j the surface's clear semi-diameter)

is formed. The surface with the largest ratio is the ghost's aperture stop, and the ray scaled by
1 / max r_j, to fill it exactly, is the ghost's marginal ray. A ghost stopped by a surface other
than the lens's own is marked `*` in the report.

The apertures a_j are the lens file's semi-diameters. Where the file gives none, see *Where the
glass ends*, section 9.

## 4. First order

With the stop found, AberrationCalculator's paraxial trace of the unfolded ghost gives its first
order, as the papers define it ([1], eqs. 5-8; Tables 5 and 6):

| Quantity | Definition |
|---|---|
| f_E | effective focal length, 1/φ with φ = −ω′/y₁ (ω′ = n′u′ of a ray entering parallel) |
| BFD | back focal distance: from the lens's last surface to the ghost's focus, −y/u′ there |
| d′ | the ghost's rear principal plane, from the last surface: BFD − f′_R, f′_R = n′/φ |
| ΔZ | from the ghost's image to the lens's image plane: positive when the ghost focuses short of the sensor |
| y′ | the ghost's marginal ray at the sensor: the radius of its disc there |
| D_ep, D_xp | the ghost's entrance and exit pupil diameters |
| L′_g, L′_g,n | from the ghost's exit pupil to the ghost's image, and to the lens's image |
| f/# | f_E / D_ep |

The ghost's disc on the sensor is its cone from the exit pupil cut at ΔZ from its focus:
|y′| = (D_xp/2)·|ΔZ| / |L′_g|. The tests check that identity; it is also what exposes a misprint
in the 2009 paper (see *Validation*).

## 5. The ghost's light

The power a ghost carries is what enters its entrance pupil times its transmittance ([2],
section 9):

    T_g = Π (1 − R_i)  over every surface crossed
        × Π R_k        over every reflection
        × η            the diffraction efficiency of its orders at the sensor (section 10), 1 otherwise

    P_g = P_in × (D_ep,g / D_ep)² × T_g          (object at infinity; the solid angle for a finite object)

P_in is the power entering the lens's own pupil (`--power`, default 1), so irradiances come out
per unit power entering. A glass-air surface reflects Fresnel's normal-incidence value,

    R = ((n₁ − n₂) / (n₁ + n₂))²,

unless coated (`--coated R`, one value for every glass-air surface). A cemented surface - glass on
both sides, a doublet's inner surface - does not reflect: the cement takes up the index step, and
what is left is too little to count, so no ghost reflects from it. `--cemented-fresnel` gives it
Fresnel's value between the two glasses instead, as if they touched without cement. The sensor
reflects `--sensor-reflectance`.

**Paraxially** the power is spread evenly over the ghost's disc ([2], eq. 17): E = P_g / (π y′²).
**By real rays** (section 7) it is spread over the real spot.

## 6. Off axis, paraxially

The ghost's paraxial chief ray - through the centre of its own stop - puts the centre of its disc
on the sensor ([1], eq. 11; [2], eqs. 17-18). Paraxial rays are linear in the field, so the ghost
lands at a fixed multiple of where the lens images the field: its **magnification** m = ȳ′_g / ȳ′.
m ≈ 1 is a ghost sitting on the image; m ≈ −1 is one mirrored through the centre of the sensor.

The **field stop** is the surface whose aperture the beam - the marginal disc about the chief ray -
reaches first as the field grows ([1], eq. 10): the smallest (a_j − |y_j|) / |ȳ_j| over the
surfaces. The field it reaches is the ghost's unvignetted field.

**Paraxial vignetting**: at each surface the beam is a disc of the marginal ray's radius about the
chief ray; the fraction of it the surface's aperture passes, at the worst surface, is the share
that gets through.

## 7. Real rays

A square grid of real rays (`--pupil`, 21 across by default) over the ghost's stop is traced
through the unfolded ghost to the sensor, each ray stopped where it misses a surface's
semi-diameter, is totally internally reflected, or lands - or, for a sensor ghost, reflects - off
the sensor's edge. The ghost's own stop is not checked: the pupil grid is what fills it.

**Ray aiming.** Each ray is aimed at the stop: launched from the point of the paraxial entrance
pupil whose real ray crosses the ghost's stop at the grid's point, times the paraxial marginal ray's
height there - found by Broyden's method from the chief ray's Jacobian, to a billionth of the stop's
radius. The paraxial pupil itself is not the real one: a fast lens's pupil aberrations make it
overfill the stop (US 8,264,785's Example 4, at F/1.6, stops an axial ray at 0.97 of it), and at
a wide field a ray launched at its centre can miss the stop's centre by much more. The real
pupil's area, from 32 aimed rays around its rim, against the paraxial pupil's, is the light the stop
lets in; it scales τ below. `--no-ray-aiming` launches the grid across the paraxial pupil instead.

From the rays that arrive: the spot's **centroid**, its **RMS radius** σ about the centroid, its
largest radius, the **share that got through** τ - of the paraxial pupil's light: the share of rays
that arrive times the real pupil's area - and where the real chief ray lands. The
irradiance is the power that arrives over a disc of radius

    r_eff = max( √2·σ,  1.22 λ N / √τ ),

√2·σ being the radius of an evenly filled disc of that RMS, and 1.22 λN the Airy radius of the
ghost's cone at the sensor (N = 1/(2|u′|)) - below which diffraction, not geometry, sets the size.
A beam cut to a fraction τ of its area diffracts as an aperture of that size, wider by 1/√τ.

**A beam vignetted to a sliver** reaches the sensor as a handful of rays whose spread says
nothing, so the grid is refined, up to 161 across, until at least 64 rays arrive.

### Where each ghost is worst

The fields are swept from the axis to 1.2 times the lens's largest field (`--field-extent`,
`--field-steps`): a bright source just outside the picture still sends its ghosts into it. Each
ghost is ranked by its **brightest** field. Because a ghost can be sharp in a narrow band of field
that falls between the sweep's steps, the real spot is also scanned finely, at 120 fields with a
coarser grid, and traced in full where it is brightest; that field is added to the ghost's table,
marked `P`.

## 8. Ghosts that focus off axis

A ghost out of focus on axis can be in focus off it, where its curved image surfaces meet the
sensor. This is the 2011 paper's section 12 [2], and it is where the worst ghosts often are.

**Third-order prediction.** The ghost's own Seidel sums, from AberrationCalculator on the unfolded
ghost at a reference field, give how far its sagittal and tangential foci stand short of its
paraxial image:

    sag_S = (S_III + S_IV) / (2 n′ u′²),     sag_T = (3 S_III + S_IV) / (2 n′ u′²)

Both grow as the square of the field (s, the field's tangent as a fraction of the reference's), so
the focus stands ΔZ + sag·s² short of the sensor, and crosses it where

    s² = −ΔZ / sag      ([2], eqs. 23-25).

**Real crossings.** Two real rays either side of the ghost's chief ray, in the plane of the field,
cross at the real tangential focus; two across it, at the sagittal - Coddington's foci, traced. A
scan over 120 fields finds where each changes sign, and bisection closes on where it is exactly on
the sensor. A focus also changes sign by passing through infinity, where the rays leave parallel;
that is told apart by the focus growing there rather than vanishing, and is not a crossing. Each
crossing is traced in full, marked `T` or `S`, and reported `(vignetted)` if none of the ghost's
light reaches it.

Both are chief-ray-local, as the papers' image surfaces are: on a fast, aberrated ghost the rays
next to the chief ray can focus on the sensor while the spot stays large. That is why the fine
scan of the real spot (section 7) is what ranks the ghosts.

## 9. The sensor, and where the glass ends

**The sensor's size.** With `--sensor W×H` given, light landing off the rectangle is not seen, and
a ghost reflecting from the sensor reflects only where the sensor is. **Given no size**, the sensor
is the lens's **image circle** - where its real chief ray of the largest field lands - so light
landing beyond what the lens images does not count. A lens with no field has no image circle, and
its sensor is left unbounded.

**Field direction.** The lens is rotationally symmetric, so the field's plane is always traced as
the meridian. `--field-direction` (height, width, diagonal, or an angle from the height) says how
that plane lies on the sensor: points are rotated into the sensor's frame to test its edges, and
the grating's orders (section 10) are rotated out of it.

**Where the glass ends.** A lens file that gives no semi-diameters is treated as a lens that has
them: each surface gets the aperture needed to pass the lens's **full beam at every field up to the
largest it specifies** - the largest height its real rays reach there, around the whole pupil rim
(64 points), at eleven fields from the axis to the largest - opened by 1 − cos(π/64), the most a
rim ray between two samples can reach beyond them. Nothing inside the lens's field is vignetted;
beyond it, and for ghost light that strays outside the lens's own beam, those are the edges of the
glass. The stop keeps the size that sets the lens's F-number. The report names the surfaces sized
this way.

Without this a ghost could reflect from a surface's sphere far outside the lens, and land far off
the sensor, and still count - which is how it was found: in the drawings.

## 10. The sensor's diffraction

A sensor's pixels repeat, so the sensor is a **reflective two-dimensional grating** with period Λ
(`--sensor-period`; the colour-filter repeat, twice the pixel pitch, for a Bayer sensor). Each
reflection from it sends light into orders (m, n), turned from the mirror direction by the grating
equation in direction cosines:

    L′ = L + m λ / (n Λx),        M′ = M + n λ / (n Λy)

(n the medium's index at the sensor; m along its width, n along its height, rotated into the
traced frame). An order with L′² + M′² ≥ 1 does not propagate. Each order is a copy of the ghost,
displaced on the sensor.

**Tracing.** AberrationCalculator's ray trace knows mirrors but not gratings, so a ghost whose
sensor reflections diffract is traced in segments split at each of them: traced to the sensor,
kicked into its order, carried to the next surface's vertex plane and traced on. Paraxially an
order is a kick to the slope at the sensor, which the rest of the ghost carries to a fixed
displacement: for a ghost that returns off a flat face a reduced distance L from the sensor, the
orders are 2 L λ/Λ apart.

**Efficiencies.** How the reflected light divides among the orders depends on the pixel's
structure, which no one publishes. By default each pixel is a reflecting aperture filling a
fraction f of the period (`--fill`, default 0.5), whose far field weights order (m, n) by

    η(m, n) ∝ sinc²(m f) · sinc²(n f),

normalised over every order that propagates, so each carries its true share; f = 1 is a plain
mirror, all its light in the zeroth order. A measured table can be given instead
(`--order-table`). Every sensor ghost becomes one ghost per order, named with it -
`G5,4 (+1,0)` - carrying η of the sensor's reflected light; one reflecting from the sensor twice
diffracts twice. `--orders` and `--min-efficiency` bound how many are analysed.

**Whether the orders show as dots.** An order's disc and the spacing between orders grow alike with
the distance of the returning surface, so their ratio depends on the F-number N alone:
spacing / disc diameter ≈ N λ / Λ. The dots separate, with a gap, when N > Λ/λ.

## 11. Several wavelengths

The lens is analysed at each wavelength (`--wavelengths`, default the lens's own with their
weights): the indices, and so each ghost's focus, size and place, change with it, and so do the
Fresnel reflectances, the grating's angles and the Airy disc. Each ghost's irradiance is added up
over the spectrum, each wavelength weighted by its share w_i of the light entering:

    E(h) = Σ w_i E_i(h) / Σ w_i

at the sweep's fields **and every field any wavelength found special** (its brightest, its
crossings), each traced at every wavelength - so the sum at a sharp off-axis peak is traced, not
interpolated. The ghosts are ranked by it, with each wavelength's share at the peak: the ghost's
colour. The rest of the report, and the drawings, are at the primary wavelength.

## 12. Drawings

`--layouts` draws the brightest ghosts in the lens, each at its worst field (or at
`--layout-field`, choosing the ghosts brightest there). The lens is drawn in section, each glass
closed by its two faces out to their own clear apertures, the larger face at its full sag and the
smaller extended to meet it - ported from LensHH-LT's layout renderer. Over it, a fan of the
ghost's real rays folded back into the lens, each leg between reflections in its own colour; the
reflecting surfaces in red; rays the ghost's apertures or the sensor stop drawn faintly to where
they are stopped; the ghost's paraxial focus; and the lens's own rays at the field, dashed, with
where they form the image.

---

## Validation

**The papers' worked example** (`tests/TestData/Maksoud_BiconvexPlate.zmx`, a biconvex BK7 lens and
a plate) is reproduced by the tests: the six ghosts' f_E, BFD, d′ and y′ (2009 Table 5), D_ep, L′_g,
ΔZ, L′_g,n, D_xp and f/# (Table 6), the ghost stops of the original lens and the ratios that choose
them (Tables 2 and 3), and the transmittances and irradiances (2011, Fig. 8).

A few printed values disagree with the paper's own other columns, and are checked against what
those give:

| Printed | Should be | Why |
|---|---|---|
| G2,1 f_E = 15.447 | 15.4447 | BFD − d′ = −40.38 + 55.825 |
| G2,1 L′_g = 23.204 | 23.2064 | L′_g,n − ΔZ = 108.837 − 85.63 |
| G4,1 y′ = −3.1169 | −3.7773 | 3.1169 is its D_xp; D_xp, L′_g, ΔZ give 3.7773 |
| G4,2 BFD = −55.5335 | −55.5334 | f_E + d′ = 88.0681 − 143.6015 |
| G4,3 f/# = 9.1733 | 9.7580 | f_E / D_ep; 9.1733 repeats D_xp's decimals |
| Table 2, G3,2: 0.02547 | 0.2547 | a four-decimal column |

and Table 2 was computed with semi-diameters 5 for the lens's back face and 2.30735 for the
plate's, not the 4.92 and 2.31 printed in Table 1.

**Beyond the papers**, where there is nothing printed to check against, the checks are
independent derivations: a sensor ghost between two flats against the lens's own chief ray carried
twice across the gap (paraxially and by real rays, to 9 decimals); a diffraction order against the
grating equation worked by hand; third-order crossings against real ones (within 1 %); defocused
ghosts' real spots against their paraxial discs; efficiencies summing to one; the image circle and
the sized apertures against the lens's own rays. Each ghost is also checked against LensHH-LT,
exported as a lens file and analysed there: see [verification](verification.md).

## Limits

- **Rotationally symmetric lenses.** Tilts and decentres are not carried into the unfolded ghosts;
  catadioptric lenses (mirrors in the lens) are refused.
- **One coated reflectance** for every glass-air surface, the same at every wavelength and angle.
- **Evenly weighted rays.** Aimed, the grid is even over the stop; a pupil distorted in object space
  sends a little more light through some parts of the stop than others, which is not weighted in.
- **Brightness is an even disc** of the spot's RMS size, floored by the Airy disc - not a map of the
  spot's structure.
- **Fields in one direction.** Each sweep runs along one line across the sensor; there is no
  two-dimensional map of it.
- **Image surfaces to third order.** AberrationCalculator's fifth- and seventh-order coefficients
  are not yet used for the prediction; the real crossings are exact.
