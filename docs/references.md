# References

The sources behind GhostAnalysis, numbered as the [method](method.md) cites them, with what each
contributes.

## The method

1. R. H. Abd El-Maksoud and J. M. Sasian, "Paraxial ghost image analysis," *Proc. SPIE* **7428**,
   742807 (2009). doi:10.1117/12.828564
   — The foundation: two-reflection ghosts enumerated as surface pairs, each unfolded into a ghost
   layout and traced paraxially for its cardinal points, stops (the fill-ratio rule of section 6),
   pupils and windows, and its size at the image; the paraxial irradiance point spread function.
   Its worked example, a biconvex lens and a plate (Tables 1-6), is GhostAnalysis's validation.

2. R. H. Abd El-Maksoud and J. M. Sasian, "Modeling and analyzing ghost images for incoherent
   optical systems," *Appl. Opt.* **50**, 2305-2315 (2011).
   — Extends [1] to N reflections (the nested loops of eq. 3), adds each ghost's fourth-order
   wavefront aberration and its sagittal and tangential image surfaces (eqs. 7-13), the paraxial
   radiometry of section 9 (eqs. 14-22), and - section 12 - the ghosts that focus on the sensor off
   axis where their image surfaces cross it (eqs. 23-25).

## Earlier work the papers build on

3. L. B. Tuckerman, "On the intensity of the light reflected from or transmitted through a pile of
   plates," *J. Opt. Soc. Am.* **37**, 818-819 (1947).

4. A. E. Murray, "Reflected light and ghosts in optical systems," *J. Opt. Soc. Am.* **39**, 30-31
   (1949). — How many distinct ghost images a system can form.

5. A. G. Naylor, "Veiling glare due to multiple reflections between surfaces," *Can. J. Phys.*
   **48**, 2720-2724 (1970). — The first use of ray tracing for a ghost's size and irradiance.

6. G. Smith, "Veiling glare due to reflections from component surfaces: the paraxial
   approximation," *Optica Acta* **18**, 815-827 (1971). — Paraxial tracing to find the surfaces
   likely to cause glare.

7. M. R. Descour et al., "Toward the development of miniaturized imaging systems for detection of
   pre-cancer," *IEEE J. Quantum Electron.* **38**, 122-130 (2002). — Ghosts limiting a real device.

8. J.-C. Perrin, "Methods for rapid evaluation of the stray light in optical systems," *Proc. SPIE*
   **5249**, 392-399 (2004).

9. J. D. Rogers, T. S. Tkaczyk, M. R. Descour, A. H. Kärkkäinen and R. Richards-Kortum, "Removal of
   ghost images by using tilted element optical systems with polynomial surfaces for aberration
   compensation," *Opt. Lett.* **31**, 504-506 (2006).

## Aberrations and ray tracing

10. W. T. Welford, *Aberrations of Optical Systems* (Adam Hilger, 1986), ch. 8. — The Seidel sums
    AberrationCalculator computes, from which each ghost's image surfaces are found (method,
    section 8).

## Sensor ghosts, and the lenses of the examples

11. M. Ando and T. Mitsuhashi (Tamron Co., Ltd.), "Imaging lens," US Patent 8,264,785 B2
    (Sep. 11, 2012). — Written for vehicle cameras, against ghosts from light reflected by the
    sensor: its criterion θ ≥ 30° on the angle between the on-axis marginal ray leaving the last
    lens surface and that surface's normal, and f/R₁ ≥ 0.3 on the first surface. Its six examples
    are the [examples](examples.md)' US 8,264,785 series, analysed from its own tables.

The following were pointed out for the physics of sensor ghosts; they were not examined for this
project, and are listed as further reading:

12. US Patent 7,663,814 (Fujinon) — a negative rearmost lens, its strongly concave last surface
    condensing sensor-reflected flare back onto the sensor; sensor-to-lens ghosts brighter than
    lens-to-lens ones, the stop hardly clipping paths behind it.

13. US Patent 4,995,708 (Canon) — sensor light reflected by a lens surface refocusing at or near the
    sensor as a ghost image, far from it as diffuse flare.

14. US Patent 4,892,397 (Canon) — a flat filter in an afocal section retro-reflecting sensor light
    to refocus near the sensor.

## Software

15. AberrationCalculator, https://github.com/jaruiz6363/AberrationCalculator — the lens model, the
    readers for ZEMAX, CODE V, OSLO, OpTaliX, Optiland and LensHH-LT files, the glass catalogs, the
    paraxial and real-ray traces and the Seidel sums, all of which GhostAnalysis builds on.

16. LensHH-LT, Synapse Optics — the drawing of the lens in the ghost layouts is ported from its
    SystemLayoutRenderer (MIT licence, copyright (c) 2026 Synapse Optics).
