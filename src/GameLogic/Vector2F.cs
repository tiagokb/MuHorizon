// <copyright file="Vector2F.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Continuous 2D position used for the Protobuf/WebSocket protocol.
/// Scale: 1 tile = 8 units; valid range 0–2040 on a 256-tile map.
/// </summary>
public readonly record struct Vector2F(float X, float Y)
{
    /// <summary>Gets the zero vector.</summary>
    public static readonly Vector2F Zero = default;

    /// <summary>Returns the tile-center position for the given tile.</summary>
    public static Vector2F FromTile(Point p) => new(p.X * 8f + 4f, p.Y * 8f + 4f);

    /// <summary>Converts to the nearest tile (truncates, does not round).</summary>
    public Point ToTile() => new((byte)(X / 8f), (byte)(Y / 8f));

    /// <summary>Squared distance to another position (avoids a sqrt).</summary>
    public float DistanceSquaredTo(Vector2F other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    public static Vector2F operator +(Vector2F a, Vector2F b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2F operator -(Vector2F a, Vector2F b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2F operator *(Vector2F v, float s) => new(v.X * s, v.Y * s);
}
