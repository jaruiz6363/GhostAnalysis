using System.Xml.Linq;
using GhostAnalysis.Cli;
using GhostAnalysis.Core.Ghosts;
using GhostAnalysis.Core.Reporting;
using Xunit;

namespace GhostAnalysis.Tests;

/// <summary>The ghosts drawn in the lens.</summary>
public class DrawingTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static readonly Lazy<GhostResult> Paper = new(() =>
        GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog, new GhostOptions { Fields = new[] { 0.0, 1.0 } }));

    [Fact]
    public void AGhostIsDrawnInTheLens()
    {
        var r = Paper.Value;
        var g = r.Find(4, 3)!;
        var doc = XDocument.Parse(GhostDrawing.Svg(r, g, 1, field: 0.0));
        var text = string.Join(" ", doc.Descendants(Svg + "text").Select(t => t.Value));
        Assert.Contains("#1: G4,3 at field 0", text);
        Assert.Contains("S3", text);
        Assert.Contains("S4", text);
        Assert.Contains("ghost focus", text);

        // Two glasses; the two reflecting faces marked; three legs of 13 rays, and the lens's own three.
        var paths = doc.Descendants(Svg + "path").ToList();
        Assert.Equal(2, paths.Count(p => (string?)p.Attribute("fill") == "#B8D4F0"));
        Assert.Equal(2, paths.Count(p => (string?)p.Attribute("stroke") == "#d62728" && (string?)p.Attribute("fill") == "none"));
        var lines = doc.Descendants(Svg + "polyline").ToList();
        Assert.Equal(3, lines.Count(l => (string?)l.Attribute("stroke") == "#bbbbbb"));
        Assert.Equal(13, lines.Count(l => (string?)l.Attribute("stroke") == "#1f77b4"));
        Assert.Equal(13, lines.Count(l => (string?)l.Attribute("stroke") == "#d62728"));
        Assert.Equal(13, lines.Count(l => (string?)l.Attribute("stroke") == "#2ca02c"));
    }

    /// <summary>The legs meet where the light reflects: each ends where the next begins.</summary>
    [Fact]
    public void TheLegsJoinAtTheReflections()
    {
        var r = Paper.Value;
        var doc = XDocument.Parse(GhostDrawing.Svg(r, r.Find(4, 1)!, field: 0.0, rays: 1));
        var legs = doc.Descendants(Svg + "polyline").Where(l => (string?)l.Attribute("stroke") != "#bbbbbb")
                      .Select(l => ((string)l.Attribute("points")!).Split(' ')).ToList();
        Assert.Equal(3, legs.Count);
        Assert.Equal(legs[0][^1], legs[1][0]);
        Assert.Equal(legs[1][^1], legs[2][0]);
    }

    /// <summary>A ray the sensor's edge stops is drawn faintly to where it is stopped, and marked there.</summary>
    [Fact]
    public void AStoppedRayIsDrawnToWhereItIsStopped()
    {
        var r = GhostAnalyzer.Analyze(Lenses.BiconvexPlate(), Lenses.Catalog,
                                      new GhostOptions { Fields = new[] { 0.0 }, Sensor = new Sensor { Width = 4, Height = 4 } });
        var doc = XDocument.Parse(GhostDrawing.Svg(r, r.Find(3, 2)!, field: 0.0));
        Assert.Contains(doc.Descendants(Svg + "polyline"), l => (string?)l.Attribute("opacity") == "0.3");
        Assert.Contains("of 13 rays drawn reach the sensor", string.Join(" ", doc.Descendants(Svg + "text").Select(t => t.Value)));
    }

    [Fact]
    public void ThePageHoldsEveryDrawing()
    {
        var r = Paper.Value;
        var page = GhostDrawing.Page(r, r.Ranked.Take(3).ToList());
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(page, "<svg ").Count);
    }
}

[Collection("console")]
public class LayoutCliTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ghost_layouts_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void LayoutsDrawsTheBrightestGhosts()
    {
        var outWas = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            int code = Program.Main(new[] { "-i", Lenses.Path("TestData/Maksoud_BiconvexPlate.zmx"), "--layouts", "3", "--layout-dir", _dir });
            Assert.Equal(0, code);
        }
        finally { Console.SetOut(outWas); }
        var svgs = Directory.GetFiles(_dir, "*.svg").Select(Path.GetFileName).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "Maksoud_BiconvexPlate_ghost1_G4-3.svg", "Maksoud_BiconvexPlate_ghost2_G4-1.svg", "Maksoud_BiconvexPlate_ghost3_G3-1.svg" }, svgs);
        Assert.True(File.Exists(Path.Combine(_dir, "Maksoud_BiconvexPlate_ghosts.html")));
        foreach (var f in Directory.GetFiles(_dir, "*.svg")) XDocument.Load(f);   // well formed
    }

    /// <summary>
    /// At a field given, the ghosts drawn are the brightest there: on the Cooke at 14 degrees not
    /// G7,2, the brightest overall, which is wholly vignetted by then, but G6,1.
    /// </summary>
    [Fact]
    public void AtAFieldTheGhostsBrightestThereAreDrawn()
    {
        var outWas = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            Assert.Equal(0, Program.Main(new[] { "-i", Lenses.Path("TestData/Cooke_40deg_FC.zmx"), "--coated", "0.005",
                                                 "--fields", "0,7,14", "--layouts", "1", "--layout-field", "14", "--layout-dir", _dir }));
        }
        finally { Console.SetOut(outWas); }
        var svg = Assert.Single(Directory.GetFiles(_dir, "*.svg"));
        Assert.EndsWith("_ghost1_G6-1.svg", svg);
        Assert.Contains("G6,1 at field 14", File.ReadAllText(svg));
    }

    [Fact]
    public void WithoutACountFiveAreDrawn()
    {
        var outWas = Console.Out;
        Console.SetOut(new StringWriter());
        try { Assert.Equal(0, Program.Main(new[] { "-i", Lenses.Path("TestData/Maksoud_BiconvexPlate.zmx"), "--layouts", "--layout-dir", _dir })); }
        finally { Console.SetOut(outWas); }
        Assert.Equal(5, Directory.GetFiles(_dir, "*.svg").Length);
    }
}

[CollectionDefinition("console", DisableParallelization = true)]
public class ConsoleCollection { }
