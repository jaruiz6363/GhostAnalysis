# GhostAnalysis

Ghost image analysis of a lens. Open a lens in any format
[AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator) reads - ZEMAX, CODE V,
OSLO, OpTaliX, Optiland, LensHH-LT - and GhostAnalysis finds every ghost it forms by an even number
of reflections, the sensor's included, and tells you which are bright, where they land, why, and
what they look like.

![A ghost that focuses on the sensor off axis](docs/images/short-sensor-G4-3.svg)

## What it does

- **Every ghost, unfolded into a lens of its own.** Each reflection path becomes a folded
  sequential lens, analysed by the same code as the lens itself: its focus, pupils, stop, size at
  the sensor and brightness - Abd El-Maksoud and Sasian's paraxial ghost analysis, reproduced to
  every printed digit of their worked example.
- **The sensor reflects**, as real sensors do - often the brightest ghosts there are.
- **Real rays, across the field.** Each ghost's real spot at every field, stopped at the glass's
  edges and the sensor's, ranked by its worst field - a bright source just outside the picture
  included.
- **Ghosts that focus off axis.** Where a ghost's image surfaces cross the sensor, predicted from
  its Seidel sums and found by real rays, and where it is brightest, found by a fine scan.
- **The sensor as a grating.** Given its pixel period, each sensor reflection diffracts into
  orders: the dot grids of red-dot flare.
- **Colour.** Each wavelength analysed, each ghost's light added up over the spectrum, with its
  colour.
- **Drawings.** The brightest ghosts drawn in the lens, their real rays leg by leg.

## Quick start

```bash
git clone --recursive https://github.com/jaruiz6363/GhostAnalysis.git
cd GhostAnalysis
dotnet build
dotnet run --project src/GhostAnalysis.Cli -- -i examples/Cooke_40deg_FC.zmx --coated 0.005 --layouts
```

That analyses a Cooke triplet's ghosts at its three wavelengths, prints the report, and writes the
five brightest as drawings with an HTML page to open. The picture above is
[example 2](docs/examples.md#2-a-ghost-that-focuses-off-axis).

A camera lens, coated, with a real sensor that diffracts:

```bash
ghost -i mylens.zmx --coated 0.005 --sensor-reflectance 0.2 --sensor 36x24 --sensor-period 6 -o ghosts.txt --layouts
```

## Documentation

- **[User guide](docs/user-guide.md)** - building, running, every option, reading the report and
  the drawings, typical runs.
- **[How it works](docs/method.md)** - the method, step by step, with its equations and its
  validation.
- **[Examples](docs/examples.md)** - the papers' lens; a ghost that focuses off axis; a Cooke
  triplet; and six vehicle-camera lenses from US 8,264,785, with a diffracting sensor, an IR filter
  and cover glass, and stopped down.
- **[References](docs/references.md)** - the papers and patents it rests on.
- **[Building](BUILDING.md)** - installing .NET and building from source.

## Authors

Javier Ruiz and Claude Code.

## License

MIT - see [LICENSE](LICENSE).
