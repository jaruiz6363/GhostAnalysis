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

The fields are swept from the axis to 1.2 times the lens's largest field - a bright source just
outside the picture still sends its ghosts into it - and each ghost is ranked by its worst field.

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

Reflectances are fractions: 0.1 is 10%. A lens file whose only field is on axis is analysed on
axis unless `--fields` is given.

## Not yet

- Where the ghosts' image surfaces cross the sensor, from their aberration coefficients (2011,
  section 12) - a quick predictor of what the real rays find.
- More than one wavelength.
- Diffraction by the sensor: the pixel array is a reflective grating, so each reflection from
  the sensor sends light into orders (m, n), each a copy of the ghost displaced on the sensor.

## Building

See [BUILDING.md](BUILDING.md). Clone with `--recursive`: AberrationCalculator is a submodule.

## Authors

Javier Ruiz and Claude Code.

## License

MIT - see [LICENSE](LICENSE).
