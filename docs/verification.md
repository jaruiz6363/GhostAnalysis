# Verification

How GhostAnalysis's numbers are checked, what agrees, to how much, and what is not checked. There
are three kinds of check:

1. [Against the papers](#1-against-the-papers): the published first order of Abd El-Maksoud and
   Sasian's example.
2. [By independent derivation](#2-by-independent-derivation): results worked out a second way,
   without the ghost machinery, in the test suite.
3. [Against LensHH-LT](#3-against-lenshh-lt), ghost by ghost: each ghost's unfolded lens exported
   and analysed there independently.

[What is not checked](#4-what-is-not-checked) and [how to rerun it](#5-rerunning-it) close the page.
References in brackets are to the [references](references.md).

---

## 1. Against the papers

The papers' example [1, 2], `examples/Maksoud_BiconvexPlate.zmx` with `--no-sensor`, gives the six
ghosts of the 2009 paper's Tables 5 and 6. Every first-order quantity - f_E, BFD, d′, y′, L′_g, ΔZ,
D_xp, f/# - matches the paper to its printed digits, except six misprints that the paper's own
other columns correct ([the method](method.md#validation) lists them). G4,3's paraxial irradiance,
0.106 W/mm² for 1 W entering, is the 2011 paper's Fig. 8. The test suite holds all of these
(`MaksoudValidationTests`).

## 2. By independent derivation

The test suite (`dotnet test`, 100 tests) checks results by a route that does not share the code
under test:

| Check | How it is derived independently | Test |
|---|---|---|
| G5,4, off the sensor and back off the plate, lands at the image plus twice the gap times the chief ray's slope | the lens's own paraxial and real traces, no ghost layout | `OffAxisTests` |
| A slow, defocused ghost's spot is the paraxial disc: RMS = radius / √2, centred on the paraxial chief ray | first-order optics | `OffAxisTests` |
| Real and paraxial vignetting cut the same ghost off at the same field | the paraxial beam-disc overlap vs real rays | `OffAxisTests` |
| Every ghost lands at a fixed multiple of the image height | paraxial linearity | `OffAxisTests` |
| Real focus crossings of the sensor agree with the third-order prediction | Seidel sagittal and tangential sags, s² = −ΔZ / sag | `FocusCrossingTests` |
| A sensor diffraction order lands where the grating equation puts it, and the orders share all the light | the grating equation on the ghost's own chief ray; the efficiencies' sum | `SensorTests` |
| Along the diagonal the field reaches the sensor's corner, and the orders turn with the sensor | the rectangle's geometry | `FieldDirectionTests` |
| The two sides of the axis are mirror images | symmetry | `OffAxisTests` |
| One wavelength of a spectrum is that wavelength analysed alone | a single-wavelength run | `SpectrumTests` |
| Auto apertures pass the whole beam at the largest field and nothing past it | tracing the lens's own rim rays | `ApertureTests` |

## 3. Against LensHH-LT

An unfolded ghost is an ordinary folded sequential lens: mirrors at its reflections, negative
thicknesses between them. `--export-layouts` writes each one as a lens file, with the ghost's own
stop and entrance pupil, the apertures GhostAnalysis stopped its rays at, and its fields and
wavelengths, and writes GhostAnalysis's own results beside them as `<lens>_ghosts.csv`. The file
asks for real ray aiming, as GhostAnalysis traces (`--no-ray-aiming` writes it off). LensHH-LT then
analyses the same lens with nothing from GhostAnalysis but the file, aiming its rays by its own
means. The files compared are in [`verification/lhlt`](../verification/lhlt).

### 3.1 The papers' lens, with its sensor

The ten ghosts of `Maksoud_BiconvexPlate.zmx` (the six of the papers and four off the sensor), on
axis and at 1°, primary wavelength. Differences are GhostAnalysis minus LensHH-LT:

| Ghost | EFL, BFL, D_xp, exit pupil | chief ray, 1° | centroid, 1° | RMS, axis | RMS, 1° |
|---|---|---|---|---|---|
| G2,1 | < 0.00005 | < 0.00005 | +0.0007 | +0.56 % | +0.56 % |
| G3,1 | < 0.00005 | < 0.00005 | −0.0001 | +0.50 % | +0.50 % |
| G3,2 | < 0.00005 | < 0.00005 | +0.0001 | +0.52 % | +0.52 % |
| G4,1 | < 0.00005 | < 0.00005 | −0.0001 | +0.50 % | +0.50 % |
| G4,2 | < 0.00005 | < 0.00005 | +0.0002 | +0.52 % | +0.52 % |
| G4,3 | < 0.00005 | < 0.00005 | < 0.00005 | +0.71 % | +0.71 % |
| G5,1 | < 0.00005 | < 0.00005 | −0.1029 | −1.51 % | +1.63 % |
| G5,2 | < 0.00005 | +0.0002 | −0.0094 | +0.52 % | +1.21 % |
| G5,3 | < 0.00005 | < 0.00005 | +0.0001 | +0.52 % | +0.52 % |
| G5,4 | < 0.00005 | < 0.00005 | < 0.00005 | +0.52 % | +0.52 % |

- **First order is exact** for every ghost: LensHH-LT's paraxial trace of the unfolded lens gives
  the same focal length, back focus, exit pupil and its diameter. (LensHH-LT measures the exit pupil
  from the image, GhostAnalysis from the last surface; the table compares them at the same point.)
- **Real rays are exact.** Chief rays agree to the digits printed, and single rays traced in both
  land at the same points.
- **The RMS of an evenly filled spot reads 0.5 % high in GhostAnalysis.** It is the pupil grid:
  a 21-across square grid over a unit disc has an RMS radius of 0.7107 against the continuous
  1/√2 = 0.7071, 0.51 % more. Ghosts whose spots are defocus discs show exactly that; G4,3, whose
  spot is spherical aberration weighted to the rim, shows a little more.
- **Vignetted ghosts' spots differ, and by sampling alone.** G5,1 and G5,2 are cut by the lens's
  rim. GhostAnalysis passes 0.962 of G5,1 on axis, LensHH-LT 253 of its 325 rays; the difference is
  where each program's grid puts rays near the vignetting edge, which falls at pupil radius 0.99.
  Ray by ray the edge is the same in both: py = 0.95 and 0.98 pass, 1.0 is stopped.
- **Aiming matters even here.** With aiming off in both, the agreement is the same, but LensHH-LT
  then keeps only 290 of 325 rays at 1° of every ghost: the stop is the lens's curved front face,
  and a rim ray launched at the paraxial pupil, tilted by the field, meets it half a micron outside
  its rim. Aimed, all 325 arrive.

### 3.2 Field curvature and the sensor crossings

`Maksoud_ShortSensor.zmx` puts the sensor where G4,3's foci cross it off axis. GhostAnalysis finds
the real crossings, by bisection of two-ray Coddington foci, at 4.3439° (tangential) and 6.3688°
(sagittal); LensHH-LT's field-curvature plot of the unfolded G4,3 crosses at 4.3414° and 6.3653°,
by linear interpolation of its 0.08° steps.

The foci agree to 0.0005 up to 4°. Past it LensHH-LT's tangential focus drifts from GhostAnalysis's,
by 0.015 at 6° and 0.065 at 8°. It is the plot, not the ray trace: two rays at py = ±0.001 traced in
LensHH-LT at 8° cross 2.21527 from the sensor, GhostAnalysis's 2.21527 exactly. LensHH-LT's plot
builds its foci differently - on axis it sits 0.001 from the paraxial focus, where both programs'
rays put it.

### 3.3 Aspheres, a finite object, and wide fields: US 8,264,785

The patent's Example 1 (conic convention ε = k) and Example 4 (ε = 1 + k), with even aspheres, a
model glass on every element, an object at 60 m, and auto apertures:

| Ghost | Check | GhostAnalysis | LensHH-LT |
|---|---|---|---|
| Ex1 G10,1 | EFL, BFL, D_xp | −7.5167, −0.0673, 21.9474 | −7.5167, −0.0673, 21.9474 |
| | exit pupil, from the image | −59.8397 − 7.9 = −67.7397 | −67.7397 |
| | aimed chief ray at 10° | −1.5302363215 | −1.5302363215 |
| Ex4 G10,9 | EFL, BFL, D_xp | 9.7127, −5.5637, 4.0550 | 9.7127, −5.5637, 4.0550 |
| | exit pupil, from the image | −12.1278 − 6.88 = −19.0078 | −19.0078 |
| Ex4 G8,7 | aimed chief ray at 10° | 3.6174887560 | 3.6174887561 |

First order and real rays agree exactly through the aspheres, the model glasses and the finite
object, and both programs aim the chief ray through the same point: the centre of the stop, which
Ex1's G10,1 passes three times.

**Why the rays are aimed.** This check is what found it. Unaimed, GhostAnalysis launched its rays
across the ghost's paraxial entrance pupil and did not clip them at the ghost's own stop. A fast
lens's pupil aberrations make the paraxial pupil overfill the real stop: in Ex4 (F/1.6) LensHH-LT,
unaimed too, stopped an axial ray at py = 0.97 at the iris, where GhostAnalysis let it through; in
Ex1 (F/2.5) the real stop ended at py ≈ 0.988. So on a fast lens GhostAnalysis counted a ring of
light the iris stops, and put its rays in the spot. Clipping them at the stop instead was worse: the
grid puts samples on the rim exactly, and losing them moved the papers' lens's 1° centroids by up
to 0.36. Aimed, the grid fills the real stop, the light is the real pupil's area, and Ex4's
brightest ghost, G8,7, is 28 % brighter on axis than unaimed: its spot loses the rim rays that the
iris really stops, and shrinks from 0.74 to 0.63 in radius. At wide fields the difference is larger:
the aimed beam through Example 6's stop uses 17-20 % more of its front element than the paraxial
pupil's.

**The pupil grid on an aberrated spot.** Ex4's G8,7 is rim-heavy - its largest radius twice its
RMS - and there the default 21-across grid reads its RMS 2 % high. It converges onto LensHH-LT's:

| Pupil grid | RMS on axis | RMS at 10° | centroid at 10° |
|---|---|---|---|
| 21 (default) | 0.44235 | 0.86324 | 2.71490 |
| 41 | 0.43315 | 0.85276 | 2.72042 |
| 81 | 0.43231 | 0.85171 | 2.72082 |
| LensHH-LT | 0.43247 | 0.85175 | 2.7206 |

For a strongly aberrated ghost, `--pupil 41` or more.

### 3.4 Colour

The papers' lens at F, d and C (`--wavelengths 0.4861327,0.5875618,0.6562725`), G4,3:

| λ | centroid at 1°, GhostAnalysis / LensHH-LT | RMS, axis | RMS, 1° |
|---|---|---|---|
| F | 1.72804 / 1.7280 | +0.63 % | +0.63 % |
| C | 1.72873 / 1.7287 | +0.80 % | +0.79 % |

The dispersion is the same in both - G4,3's RMS falls from 0.096 in the blue to 0.041 in the red
in each - and the RMS difference is the grid's, as at d.

### 3.5 The other formats, and OpTaliX and OSLO themselves

`--export-format` writes the same ghosts for other lens programs. Exported in each format and read
back into LensHH-LT, Example 1's G10,1 - aspheres, model glasses, a finite object, three passes
through the stop - and the papers' G4,3 give:

| Format | Read back into LensHH-LT |
|---|---|
| ZEMAX `.zmx` | exact: first order, the aimed chief ray to every digit, apertures, ray aiming |
| OpTaliX `.otx` | exact, ray aiming included |
| CODE V `.seq` | the lens and first order exact; ray aiming comes back off (CODE V aims at the real stop by default) |
| OSLO `.len` | the lens exact; a finite object is written as an object height and NA, the height's sign opposite to the field angle's, so the ghost is mirrored across the axis; the exit pupil in the 4th decimal |
| Optiland `.json` | not usable yet: the model glasses are written beside the file, not installed, and read as air; no apertures; a finite object read as infinite |

**In OpTaliX itself**, five exported ghosts - the papers' G4,3 and G2,1, Example 1's G10,1,
Example 4's G10,9 and G8,7 - give GhostAnalysis's focal lengths. That
check found a fault LensHH-LT's reader had hidden: OpTaliX names the glass the light goes on in on a
mirror inside glass, and the writer left it off, so the three ghosts reflected inside glass came
out with the wrong focal length. AberrationCalculator now writes it, and reads it.

**In OSLO itself**, the papers' G4,3 and G2,1 - both reflected inside glass, and the two ghosts
small enough for a ten-surface OSLO - give GhostAnalysis's focal lengths: OSLO carries the glass
through a mirror, as written.

## 4. What is not checked

LensHH-LT analyses an ordinary lens; what makes a ghost more than one is GhostAnalysis's alone:

- **Sensor diffraction.** No lens file carries the sensor's grating; an exported diffracted order is
  written as its path's zeroth order. The orders are checked against the grating equation
  (section 2), not against another program.
- **Radiometry.** Fresnel reflectances, coatings, the sensor's reflectance, sinc² order efficiencies
  and irradiance are GhostAnalysis's own; the papers' G4,3 irradiance (section 1) is the one
  external check.
- **The sensor's edges.** Light landing off the sensor is lost in GhostAnalysis and not in the
  exported lens, whose image surface is unbounded. A ghost whose light spills off the sensor passes
  more rays in LensHH-LT for that reason alone.
- **Vignetted spots**, beyond their edges: two different pupil grids sample a cut beam differently
  (section 3.1).

## 5. Rerunning it

Export the ghosts, then analyse the files in LensHH-LT:

```bash
ghost -i examples/Maksoud_BiconvexPlate.zmx --fields 0,1 --wavelengths primary \
      --export-layouts verification/lhlt/paper
```

For the papers' lens, LensHH-LT's values, its rays aimed as the files ask, are recorded in
[`verification/lhlt/paper/lenshh.csv`](../verification/lhlt/paper/lenshh.csv), and
[`verification/compare.ps1`](../verification/compare.ps1) prints the differences of section 3.1:

```powershell
pwsh verification/compare.ps1 -Folder verification/lhlt/paper -ImageDistance 45.25 -Field 1
```

`--export-format zmx` (or `seq`, `len`, `otx`) writes the ghosts for other lens programs instead.
