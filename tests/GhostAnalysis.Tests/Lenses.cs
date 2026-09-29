using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;

namespace GhostAnalysis.Tests;

/// <summary>The lenses the tests analyse.</summary>
internal static class Lenses
{
    private static readonly Lazy<GlassCatalog> _catalog = new(CatalogLocator.LoadBundled);
    public static GlassCatalog Catalog => _catalog.Value;

    public static string Path(string relative) => System.IO.Path.Combine(AppContext.BaseDirectory, relative);

    public static OpticalSystem Read(string relative) => LensFile.Read(Path(relative), Catalog);

    /// <summary>
    /// Abd El-Maksoud and Sasian's example, SPIE 7428 (2009) Table 4 and Appl. Opt. 50 (2011)
    /// Table 1: a biconvex BK7 lens, R 100/-100, 5 thick, stop at its front, and a 1 mm BK7
    /// plate 50 behind it, object at infinity, EPD 10. The plate is the 2009 paper's enlarged
    /// one (semi-diameter 20), so every ghost is stopped by the lens's own stop.
    /// </summary>
    public static OpticalSystem BiconvexPlate() => Read("TestData/Maksoud_BiconvexPlate.zmx");
}
