// <copyright file="LineOfSight.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Bresenham line-of-sight check over the terrain walk map.
/// </summary>
internal static class LineOfSight
{
    /// <summary>
    /// Returns true if there are no blocking tiles between <paramref name="from"/> and <paramref name="to"/>.
    /// Both coordinates are tile positions (Point).
    /// </summary>
    internal static bool HasLineOfSight(Point from, Point to, GameMap map)
    {
        var walkMap = map.Terrain.WalkMap;
        int x0 = from.X, y0 = from.Y;
        int x1 = to.X,   y1 = to.Y;
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if ((uint)x0 >= (uint)walkMap.GetLength(0) || (uint)y0 >= (uint)walkMap.GetLength(1))
            {
                return false;
            }

            if (!walkMap[x0, y0])
            {
                return false;
            }

            if (x0 == x1 && y0 == y1)
            {
                return true;
            }

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx)  { err += dx; y0 += sy; }
        }
    }
}
