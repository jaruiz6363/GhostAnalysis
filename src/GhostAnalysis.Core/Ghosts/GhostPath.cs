namespace GhostAnalysis.Core.Ghosts;

/// <summary>
/// One ghost: the surfaces it reflects from, in the order the light meets them.
///
/// <para>Surfaces are numbered as the lens numbers them, 1 to M for the optical surfaces and M+1
/// for the image - the sensor, which reflects too. A ghost that reaches the image has an even
/// number of reflections: the first sends the light backwards, the second forwards again, and
/// so on, so the surfaces alternate k1 &gt; k2 &lt; k3 &gt; k4 ...</para>
///
/// <para>The notation is Abd El-Maksoud and Sasian's: G4,3 reflects from surface 4, then from
/// surface 3, and goes on to the image.</para>
/// </summary>
public sealed class GhostPath : IEquatable<GhostPath>
{
    public IReadOnlyList<int> Surfaces { get; }

    public GhostPath(params int[] surfaces)
    {
        if (surfaces == null || surfaces.Length == 0 || surfaces.Length % 2 != 0)
            throw new ArgumentException("A ghost that reaches the image reflects an even number of times.", nameof(surfaces));
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] < 1) throw new ArgumentException($"Surface {surfaces[i]} cannot reflect: the object is surface 0.", nameof(surfaces));
            if (i > 0)
            {
                // Odd reflections turn the light back, so the next one is nearer the object;
                // even ones turn it forward again.
                bool back = i % 2 == 1;
                if (back ? surfaces[i] >= surfaces[i - 1] : surfaces[i] <= surfaces[i - 1])
                    throw new ArgumentException($"G{string.Join(",", surfaces)} is not a path light can take.", nameof(surfaces));
            }
        }
        Surfaces = surfaces.ToArray();
    }

    public int Reflections => Surfaces.Count;

    public override string ToString() => "G" + string.Join(",", Surfaces);

    public bool Equals(GhostPath? other) => other != null && Surfaces.SequenceEqual(other.Surfaces);
    public override bool Equals(object? obj) => Equals(obj as GhostPath);
    public override int GetHashCode() => Surfaces.Aggregate(17, (h, s) => h * 31 + s);

    /// <summary>
    /// Every ghost of a lens with <paramref name="opticalSurfaces"/> surfaces, formed by
    /// <paramref name="reflections"/> reflections: the nested loops of Abd El-Maksoud and Sasian
    /// (2011), eq. 3. k1 runs 2..M, k2 1..k1-1, k3 k2+1..M, k4 1..k3-1, and so on.
    /// </summary>
    /// <param name="includeImage">Whether the image, surface M+1, reflects. It does: a sensor
    /// reflects several per cent of what reaches it. The papers leave it out.</param>
    public static IEnumerable<GhostPath> Enumerate(int opticalSurfaces, int reflections = 2, bool includeImage = true)
    {
        if (reflections < 2 || reflections % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(reflections), "A ghost that reaches the image reflects an even number of times.");
        int top = includeImage ? opticalSurfaces + 1 : opticalSurfaces;
        var k = new int[reflections];
        return Walk(k, 0, top);
    }

    private static IEnumerable<GhostPath> Walk(int[] k, int depth, int top)
    {
        if (depth == k.Length)
        {
            yield return new GhostPath(k);
            yield break;
        }
        // A reflection sending the light back needs a surface behind it to come back to.
        int from = depth == 0 ? 2 : depth % 2 == 1 ? 1 : k[depth - 1] + 1;
        int to = depth % 2 == 1 ? k[depth - 1] - 1 : top;
        for (int s = from; s <= to; s++)
        {
            k[depth] = s;
            foreach (var g in Walk(k, depth + 1, top)) yield return g;
        }
    }
}
