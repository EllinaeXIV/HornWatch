using System;
using System.Numerics;

namespace Hornwatch.Core;

public static class GroundDistance
{
    extension(Vector3 point)
    {
        public float GroundDistanceTo(Vector3 other)
        {
            var dx = point.X - other.X;
            var dz = point.Z - other.Z;

            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        public Vector3 AtHeightOf(Vector3 other) => point with { Y = other.Y };
    }
}
