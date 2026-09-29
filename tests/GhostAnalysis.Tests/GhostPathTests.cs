using GhostAnalysis.Core.Ghosts;
using Xunit;

namespace GhostAnalysis.Tests;

public class GhostPathTests
{
    [Theory]
    [InlineData(4, false, 6)]     // M(M-1)/2, the papers' count
    [InlineData(4, true, 10)]     // and with the sensor, (M+1)M/2
    [InlineData(10, false, 45)]
    public void TwoReflectionGhostsArePairs(int m, bool image, int count)
    {
        Assert.Equal(count, GhostPath.Enumerate(m, 2, image).Count());
    }

    [Fact]
    public void TheSensorGhostsReflectFromTheImageFirst()
    {
        var sensor = GhostPath.Enumerate(4, 2, true).Where(g => g.Surfaces[0] == 5).Select(g => g.ToString());
        Assert.Equal(new[] { "G5,1", "G5,2", "G5,3", "G5,4" }, sensor);
    }

    /// <summary>
    /// Four reflections alternate back and forward: 2011, eq. 2. For a single lens of two
    /// surfaces there is one, G2,1,2,1: the light rattles inside it twice.
    /// </summary>
    [Fact]
    public void FourReflectionsAlternate()
    {
        Assert.Equal(new[] { "G2,1,2,1" }, GhostPath.Enumerate(2, 4, false).Select(g => g.ToString()));
        foreach (var g in GhostPath.Enumerate(5, 4, true))
        {
            var k = g.Surfaces;
            Assert.True(k[0] > k[1] && k[1] < k[2] && k[2] > k[3], g.ToString());
        }
        // Count by brute force.
        int m = 5, top = 6, brute = 0;
        for (int a = 1; a <= top; a++)
        for (int b = 1; b < a; b++)
        for (int c = b + 1; c <= top; c++)
        for (int d = 1; d < c; d++) brute++;
        Assert.Equal(brute, GhostPath.Enumerate(m, 4, true).Count());
    }

    [Fact]
    public void APathLightCannotTakeIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new GhostPath(2, 3));
        Assert.Throws<ArgumentException>(() => new GhostPath(3));
        Assert.Throws<ArgumentException>(() => new GhostPath(3, 1, 1, 0));
    }
}
