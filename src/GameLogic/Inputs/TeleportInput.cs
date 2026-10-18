// <copyright file="TeleportInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Processed by the tick loop: casts the Teleport skill to a target tile. Requires the learned
/// skill, a walkable non-safezone destination within the skill's range and enough mana.
/// </summary>
public sealed class TeleportInput : IPlayerInput
{
    private const ushort TeleportSkillNumber = 6;

    private readonly Point _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeleportInput"/> class.
    /// </summary>
    /// <param name="target">The destination tile.</param>
    public TeleportInput(Point target) => _target = target;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        // Rooted, stunned or asleep: no movement skill.
        if (!player.IsAlive || player.IsTeleporting || player.ActiveCast is not null
            || !SkillCastRules.CanAttack(player) || SkillCastRules.IsCrowdControlled(player))
        {
            return;
        }

        if (player.CurrentMap is not { } map
            || SkillCastRules.GetCastableSkill(player, TeleportSkillNumber)?.Skill is not { } skill)
        {
            return;
        }

        // Aimed at a wall, a stone or someone's feet: land on the nearest free tile instead of not
        // teleporting at all (which can be deadly while fleeing).
        var range = skill.Range + SkillCastRules.RangeTolerance;
        if (FindLandingTile(map, player, _target, range) is not { } target)
        {
            return;
        }

        if (!await player.TryConsumeForSkillAsync(skill).ConfigureAwait(false))
        {
            return;
        }

        // Not awaited: the teleport animation takes ~2 seconds and must not stall the map's tick.
        // TeleportAsync sets IsTeleporting before its first await, so a second request in a
        // later tick is rejected above.
        _ = player.TeleportAsync(target, skill);
    }

    private const int MaxSnapDistance = 3;

    private static Point? FindLandingTile(GameMap map, Player player, Point aimed, int range)
    {
        bool CanLand(int x, int y) =>
            x is >= 0 and <= byte.MaxValue && y is >= 0 and <= byte.MaxValue
            && map.Terrain.WalkMap[x, y]
            && !map.Terrain.SafezoneMap[x, y]
            && player.IsInRange(new Point((byte)x, (byte)y), range);

        if (CanLand(aimed.X, aimed.Y))
        {
            return aimed;
        }

        // Rings around the aimed tile; the first ring with a free tile wins, closest first.
        for (var distance = 1; distance <= MaxSnapDistance; distance++)
        {
            Point? best = null;
            var bestSq = int.MaxValue;
            for (var dx = -distance; dx <= distance; dx++)
            {
                for (var dy = -distance; dy <= distance; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance || !CanLand(aimed.X + dx, aimed.Y + dy))
                    {
                        continue;
                    }

                    var sq = (dx * dx) + (dy * dy);
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = new Point((byte)(aimed.X + dx), (byte)(aimed.Y + dy));
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }
}
