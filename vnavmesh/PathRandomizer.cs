using System;
using System.Collections.Generic;
using System.Numerics;

namespace Navmesh;

public static class PathRandomizer
{
    private static readonly int _sessionSeed = Guid.NewGuid().GetHashCode();

    public static List<Vector3> ApplyCorridorRandomness(List<Vector3> originalPath, float width, float noiseScale)
    {
        if (originalPath.Count < 3 || width <= 0)
            return originalPath;

        var result = new List<Vector3>(originalPath.Count) { originalPath[0] };

        for (int i = 1; i < originalPath.Count - 1; i++)
        {
            var prev = originalPath[i - 1];
            var curr = originalPath[i];
            var next = originalPath[i + 1];

            // Calculate tangent direction
            var tangent = Vector3.Normalize(next - prev);

            // Calculate perpendicular vector (horizontal plane only)
            var perpendicular = new Vector3(-tangent.Z, 0, tangent.X);

            // Calculate cumulative distance for noise input
            float dist = 0;
            for (int j = 1; j <= i; j++)
                dist += Vector3.Distance(originalPath[j - 1], originalPath[j]);

            // Layered sine waves for natural movement
            float factor = (MathF.Sin(dist * noiseScale + _sessionSeed) +
                           MathF.Sin(dist * noiseScale * 3.0f + _sessionSeed * 2)) * 0.5f;

            var newPos = curr + perpendicular * factor * width;
            result.Add(newPos);
        }

        result.Add(originalPath[^1]);
        return result;
    }
}
