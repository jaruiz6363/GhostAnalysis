# GhostAnalysis

Ghost image analysis of a lens. Open a lens in any format
[AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator) reads - ZEMAX, CODE V,
OSLO, OpTaliX, Optiland, LensHH-LT - and every ghost it forms by an even number of reflections
is found, unfolded into a lens of its own, and analysed: where it focuses, how large it is at
the image, what stops it, and how bright it is.

## How it works

The method is Abd El-Maksoud and Sasian's paraxial ghost analysis:

- R. H. Abd El-Maksoud and J. M. Sasian, "Paraxial ghost image analysis," Proc. SPIE 7428,
  742807 (2009).
- R. H. Abd El-Maksoud and J. M. Sasian, "Modeling and analyzing ghost images for incoherent
  optical systems," Appl. Opt. 50, 2305-2315 (2011).

1. **The ghosts.** For a lens of M surfaces, a two-reflection ghost Gk1,k2 reflects from
   surface k1 back to surface k2 (k1 > k2) and goes on to the image; four reflections alternate
   k1 > k2 < k3 > k4, and so on.
2. **The sensor reflects.** The papers stop at surface M. Here the image, surface M+1, reflects
   too - a sensor sends back several per cent of what reaches it - with a reflectance you give
   (`--sensor-reflectance`, default 0.05), since no lens file says what it is.
3. **Unfolding.** Each ghost becomes a folded sequential lens: the reflecting surfaces are
   mirrors, and the stretch the light runs backwards is the lens's own prescription reversed,
   with negative thicknesses. AberrationCalculator traces and analyses a folded lens like any
   other, so each ghost gets the full first order - focal length, back focus, principal
   planes, pupils - from the same code that analyses the lens.
4. **The ghost's stop.** An axial ray is traced through the unfolded ghost; the surface it
   fills most, height over semi-diameter, is the ghost's aperture stop. It need not be the
   lens's own (the report marks those with `*`).
5. **Its light.** What enters the ghost's pupil, times a Fresnel transmittance at every
   surface crossed and a reflectance at every reflection, spread over the area the ghost
   covers on the sensor. Uncoated by default; `--coated R` gives every glass-air surface
   reflectance R.
6. **Off axis, paraxially.** The ghost's chief ray, through the centre of its own stop, puts
   the centre of its disc on the sensor - at a fixed multiple of the image height, its
   magnification (-1 is a ghost mirrored through the centre). The surface that first cuts its
   beam as the field grows is its field stop, and at each field the share of its beam every
   surface passes gives its vignetting.
7. **Off axis, by real rays.** A grid of real rays across the ghost's entrance pupil is traced
   through the unfolded ghost to the sensor, each ray stopped where it misses a surface's
   semi-diameter. Where the ghost really lands, its real spot size and how much of it gets
   through come from those; the irradiance is its power over a disc of √2 × its RMS radius, no
   smaller than the Airy disc of its cone. This is where a ghost that is out of focus on axis
   is found to focus on the sensor off axis.

8. **Where it focuses off axis.** A ghost out of focus on axis can be in focus off it, where
   its curved image surfaces meet the sensor (2011, section 12). Its own Seidel sums give its
   tangential and sagittal image surfaces, and so the fields where they cross the sensor -
   the prediction. Real rays either side of its chief ray give its real tangential and
   sagittal foci at any field; a fine scan and bisection find where each is exactly on the
   sensor - the crossings - and the spot is traced there too.
9. **Where it is brightest.** The foci next to the chief ray speak for the middle of the beam
   only; a fast, aberrated ghost can have them on the sensor and its spot no smaller. So the
   real spot itself is scanned finely over field, and traced in full where it is brightest,
   wherever that falls between the fields of the sweep.

10. **The sensor's size.** Given one (`--sensor 36x24`), light landing off it is not seen, and a
    ghost that reflects from the sensor reflects only where the sensor is.
11. **The sensor's diffraction.** Its pixels repeat, so it is a reflective grating: each
    reflection from it sends light into orders (m, n), turned by the grating equation
    L' = L + mλ/Λx, M' = M + nλ/Λy, each a copy of the ghost displaced on the sensor. Given its
    period (`--sensor-period`, the pixel pitch, or twice it for a Bayer colour sensor), every
    sensor ghost becomes one ghost per order, traced in segments with the grating's kick at
    each reflection from the sensor. How the reflected light divides among the orders depends
    on the pixel's structure; by default each pixel is a reflecting aperture filling a fraction
    f of the period (`--fill`, default 0.5), weighting order (m, n) by sinc²(mf) sinc²(nf),
    normalised over the orders that propagate - f = 1 is a mirror. A measured table can be
    given instead (`--order-table`, lines of `m, n, efficiency`).

The fields are swept from the axis to 1.2 times the lens's largest field - a bright source just
outside the picture still sends its ghosts into it - and each ghost is ranked by its worst field.

The paper's lens with its sensor moved to 43.0 behind the plate, just short of the plate ghost
G4,3's focus, is the test of this: the third order puts its tangential and sagittal surfaces on
the sensor at 4.33 and 6.33 degrees, real rays at 4.34 and 6.37, and the ghost is brightest at
3.75 degrees, nearly four times as bright as on axis.

## Validation

The papers' worked example - a biconvex BK7 lens and a plate - is in
`tests/TestData/Maksoud_BiconvexPlate.zmx`, and the tests reproduce its tables: the six ghosts'
focal lengths, back focal distances, principal planes, sizes at the image (2009 Table 5),
pupils, defocus and F-numbers (Table 6), the ghost stops of the original lens and the ratios
that choose them (Tables 2 and 3), and the transmittances and irradiances (2011, Fig. 8). The
handful of printed values that disagree with the paper's own other columns are listed in
`MaksoudValidationTests.cs`.

## Usage

```bash
ghost -i lens.zmx
ghost -i lens.seq --coated 0.005 --sensor-reflectance 0.1 -o ghosts.txt
ghost -i lens.len --no-sensor -n 4
```

| Option | |
|---|---|
| `-i`, `--input` | the lens |
| `-o`, `--output` | write the report there as well |
| `-n`, `--reflections` | reflections per ghost: 2 (default), 4, ... |
| `--sensor-reflectance R` | the sensor's reflectance (default 0.05) |
| `--no-sensor` | the sensor does not reflect, as in the papers |
| `--coated R` | every glass-air surface reflects R (default: uncoated Fresnel) |
| `--power P` | power entering the lens (default 1) |
| `--fields f1,f2,...` | the fields to analyse, in the lens's field units |
| `--field-extent x` | sweep to x times the lens's largest field (default 1.2) |
| `--field-steps n` | steps in the sweep (default 12) |
| `--pupil n` | real rays across each ghost's pupil (default 21) |
| `--paraxial` | no real rays: the papers' paraxial analysis only |
| `--detail n` | show the n brightest ghosts field by field (default 5) |
| `--sensor WxH` | the sensor's size, in lens units: width across the field's plane, height along it |
| `--sensor-period P[xQ]` | its grating period in micrometres; the sensor then diffracts |
| `--fill f` | each pixel's reflecting aperture as a fraction of the period (default 0.5) |
| `--order-table file` | measured order efficiencies instead: lines of `m, n, efficiency` |
| `--orders n` | the largest \|m\| and \|n\| analysed (default 2) |
| `--min-efficiency e` | leave out orders with less than e of the reflected light (default 0.001) |

Reflectances are fractions: 0.1 is 10%. A lens file whose only field is on axis is analysed on
axis unless `--fields` is given.

## Not yet

- More than one wavelength.
- Image surfaces beyond third order, from AberrationCalculator's fifth- and seventh-order
  coefficients.
- Fields off the meridian: the sweep runs along the sensor's height; a diagonal or a
  two-dimensional map of the sensor is not yet traced.

## Building

See [BUILDING.md](BUILDING.md). Clone with `--recursive`: AberrationCalculator is a submodule.

## Authors

Javier Ruiz and Claude Code.

## License

MIT - see [LICENSE](LICENSE).
