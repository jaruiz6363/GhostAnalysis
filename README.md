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
   surface crossed and a reflectance at every reflection, spread evenly over the ghost's disc
   at the image. Uncoated by default; `--coated R` gives every glass-air surface reflectance R.

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

## Not yet

- Off-axis ghosts: the ghost's own chief ray, field stop and windows, and where its image
  surfaces cross the sensor (2011, section 12).
- The ghosts' aberrations, through seventh order, and their real-ray spots.
- Diffraction by the sensor: the pixel array is a reflective grating, so each reflection from
  the sensor sends light into orders (m, n), each a copy of the ghost displaced on the sensor.

## Building

See [BUILDING.md](BUILDING.md). Clone with `--recursive`: AberrationCalculator is a submodule.

## Authors

Javier Ruiz and Claude Code.

## License

MIT - see [LICENSE](LICENSE).
