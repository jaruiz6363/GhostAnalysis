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
        "  --power <P>                   power entering the lens (default 1)\n";

    public static int Main(string[] args)
    {
        try
        {
            string? input = null, output = null;
            int reflections = 2;
            double sensor = 0.05, power = 1.0;
            double? coated = null;
            bool noSensor = false;
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
            });
            string report = Report.Write(result);
            Console.Write(report);
            if (output != null)
            {
                File.WriteAllText(output, report);
                Console.WriteLine($"Written: {output}");
            }
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IOException or NotSupportedException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }
}
