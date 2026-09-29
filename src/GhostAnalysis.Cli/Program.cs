using System.Globalization;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using GhostAnalysis.Core.Ghosts;
using GhostAnalysis.Core.Reporting;

namespace GhostAnalysis.Cli;

public static class Program
{
    private const string Usage =
        "ghost -i <lens> [options]\n" +
        "\n" +
        "Reads a lens in any format AberrationCalculator reads (ZEMAX, CODE V, OSLO, OpTaliX,\n" +
        "Optiland, LensHH-LT) and analyses its ghosts.\n" +
        "\n" +
        "  -i, --input <file>            the lens\n" +
        "  -o, --output <file>           write the report there as well\n" +
        "  -n, --reflections <2|4|...>   reflections per ghost (default 2)\n" +
        "  --sensor-reflectance <R>      the sensor's reflectance (default 0.05)\n" +
        "  --no-sensor                   the sensor does not reflect, as in the papers\n" +
        "  --coated <R>                  every glass-air surface reflects R (default: uncoated Fresnel)\n" +
        "  --power <P>                   power entering the lens (default 1)\n" +
        "  --fields <f1,f2,...>          the fields to analyse, in the lens's field units\n" +
        "                                (default: the axis to 1.2 x the lens's largest field, in 12 steps)\n" +
        "  --field-extent <x>            sweep to x times the lens's largest field (default 1.2)\n" +
        "  --field-steps <n>             steps in the sweep (default 12)\n" +
        "  --pupil <n>                   rays across each ghost's pupil (default 21)\n" +
        "  --paraxial                    no real rays: the papers' paraxial analysis only\n" +
        "  --detail <n>                  show the n brightest ghosts field by field (default 5)\n" +
        "  --layouts [n]                 draw the n brightest ghosts in the lens (default 5): an SVG\n" +
        "                                each, and one HTML page with them all\n" +
        "  --layout-dir <dir>            where to write them (default: beside -o, or here)\n" +
        "  --layout-field <f>            draw them all at this field (default: each at its brightest)\n" +
        "\n" +
        "The sensor:\n" +
        "  --sensor <W>x<H>              its size, in lens units (mm): width across the field's plane,\n" +
        "                                height along it. Light off it is not seen, nor reflected.\n" +
        "  --sensor-period <P>[x<Q>]     its grating period, in micrometres: the pixel pitch, or for a\n" +
        "                                Bayer colour sensor twice it. The sensor then diffracts.\n" +
        "  --fill <f>                    each pixel's reflecting aperture as a fraction of the period,\n" +
        "                                for the order efficiencies sinc²(m f) sinc²(n f) (default 0.5)\n" +
        "  --order-table <file>          measured order efficiencies instead: lines of 'm, n, efficiency'\n" +
        "  --orders <n>                  the largest |m| and |n| analysed (default 2)\n" +
        "  --min-efficiency <e>          leave out orders carrying less than e of the reflected light\n" +
        "                                (default 0.001)\n" +
        "\n" +
        "Reflectances are fractions: 0.1 is 10%.\n";

    public static int Main(string[] args)
    {
        try
        {
            string? input = null, output = null;
            int reflections = 2;
            double sensor = 0.05, power = 1.0;
            double? coated = null;
            bool noSensor = false, paraxial = false;
            List<double>? fields = null;
            double extent = 1.2;
            int steps = 12, pupil = 21, detail = 5, layouts = 0;
            string? layoutDir = null;
            double? layoutField = null;
            double? width = null, height = null, periodX = null, periodY = null;
            double fill = 0.5, minEfficiency = 1e-3;
            int maxOrder = 2;
            Dictionary<(int, int), double>? table = null;
            (double, double) Pair(string text)
            {
                var parts = text.ToLowerInvariant().Split('x');
                double a = double.Parse(parts[0], CultureInfo.InvariantCulture);
                return (a, parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : a);
            }
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
                switch (args[i])
                {
                    case "-i": case "--input": input = Next(); break;
                    case "-o": case "--output": output = Next(); break;
                    case "-n": case "--reflections": reflections = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--sensor-reflectance": sensor = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--no-sensor": noSensor = true; break;
                    case "--coated": coated = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--power": power = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--fields":
                        fields = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                       .Select(f => double.Parse(f, CultureInfo.InvariantCulture)).ToList();
                        break;
                    case "--field-extent": extent = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--field-steps": steps = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--pupil": pupil = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--paraxial": paraxial = true; break;
                    case "--detail": detail = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--layouts":
                        // The count is optional: a number next, or 5.
                        layouts = i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                            ? int.Parse(args[++i], CultureInfo.InvariantCulture) : 5;
                        break;
                    case "--layout-dir": layoutDir = Next(); break;
                    case "--layout-field": layoutField = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--sensor": (width, height) = Pair(Next()); break;
                    case "--sensor-period": (periodX, periodY) = Pair(Next()); break;
                    case "--fill": fill = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--order-table": table = Sensor.ReadEfficiencies(Next()); break;
                    case "--orders": maxOrder = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--min-efficiency": minEfficiency = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "-h": case "--help": Console.WriteLine(Usage); return 0;
                    default: throw new ArgumentException($"'{args[i]}' is not an option.");
                }
            }
            if (input == null) { Console.WriteLine(Usage); return 1; }

            var catalog = CatalogLocator.LoadBundled();
            var lens = LensFile.Read(input, catalog);
            var result = GhostAnalyzer.Analyze(lens, catalog, new GhostOptions
            {
                Reflections = reflections,
                ImageReflects = !noSensor,
                ImageReflectance = sensor,
                CoatedReflectance = coated,
                InputPower = power,
                Fields = fields,
                FieldExtent = extent,
                FieldSteps = steps,
                PupilSamples = pupil,
                RealRays = !paraxial,
                Sensor = new Sensor
                {
                    Width = width, Height = height, PeriodX = periodX, PeriodY = periodY,
                    FillFactor = fill, Efficiencies = table, MaxOrder = maxOrder, MinimumEfficiency = minEfficiency,
                },
            });
            string report = Report.Write(result, detail);
            Console.Write(report);
            if (output != null)
            {
                File.WriteAllText(output, report);
                Console.WriteLine($"Written: {output}");
            }
            if (layouts > 0) WriteLayouts(result, input, layoutDir ?? Path.GetDirectoryName(Path.GetFullPath(output ?? "x")) ?? ".", layouts, layoutField);
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or NotSupportedException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }

    /// <summary>The brightest ghosts drawn in the lens: an SVG each, and a page with them all.</summary>
    private static void WriteLayouts(GhostResult result, string input, string dir, int count, double? field)
    {
        Directory.CreateDirectory(dir);
        string stem = Path.GetFileNameWithoutExtension(input);
        // At a field given, the ghosts brightest there - at the nearest field analysed; a ghost
        // bright elsewhere may be wholly vignetted at it. Otherwise the brightest at their worst.
        var ghosts = (field is double f
            ? result.Ghosts.OrderByDescending(g => g.Fields.MinBy(x => Math.Abs(x.Field - f))?.Brightness ?? 0.0)
            : result.Ranked).Take(count).ToList();
        for (int i = 0; i < ghosts.Count; i++)
        {
            string name = new string(ghosts[i].Name.Select(ch => char.IsLetterOrDigit(ch) || ch is '+' or '-' or '(' or ')' ? ch : ch == ',' ? '-' : '_').ToArray());
            string file = Path.Combine(dir, $"{stem}_ghost{i + 1}_{name}.svg");
            File.WriteAllText(file, GhostDrawing.Svg(result, ghosts[i], i + 1, field));
            Console.WriteLine($"Written: {file}");
        }
        string page = Path.Combine(dir, $"{stem}_ghosts.html");
        File.WriteAllText(page, GhostDrawing.Page(result, ghosts, field));
        Console.WriteLine($"Written: {page}");
    }
}
