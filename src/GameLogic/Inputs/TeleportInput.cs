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
        if (!player.IsAlive || player.IsTeleporting || player.ActiveCast is not null || !SkillCastRules.CanAttack(player))
        {
            return;
        }

        if (player.CurrentMap is not { } map
            || !map.Terrain.WalkMap[_target.X, _target.Y]
            || map.Terrain.SafezoneMap[_target.X, _target.Y])
        {
            return;
        }

        if (SkillCastRules.GetCastableSkill(player, TeleportSkillNumber)?.Skill is not { } skill
            || !player.IsInRange(_target, skill.Range + SkillCastRules.RangeTolerance))
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
        _ = player.TeleportAsync(_target, skill);
    }
}
