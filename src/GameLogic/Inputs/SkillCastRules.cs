// <copyright file="SkillCastRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Server-side checks shared by the attack and skill inputs, so every cast path
/// (targeted, projectile, area, teleport, basic attack) enforces the same rules.
/// </summary>
internal static class SkillCastRules
{
    /// <summary>
    /// Extra tiles accepted beyond <see cref="Skill.Range"/>: positions seen by the client lag
    /// behind the server by up to two tiles (same tolerance as TargetedSkillDefaultPlugin).
    /// </summary>
    internal const int RangeTolerance = 2;

    /// <summary>
    /// Gets the player's own entry of the skill, or <c>null</c> when the skill is not learned
    /// or passive. Unlearned skills can't be cast — the client may request any skill number.
    /// </summary>
    internal static SkillEntry? GetCastableSkill(Player player, ushort skillId) =>
        player.SkillList?.GetSkill(skillId) is { Skill: { SkillType: not SkillType.PassiveBoost } } entry
        && SkillLoadout.IsEquipped(player, skillId)
            ? entry
            : null;

    /// <summary>
    /// Gets a value indicating whether the player is able to attack: alive, not stunned, asleep or in a safezone.
    /// </summary>
    internal static bool CanAttack(Player player) =>
        player.IsAlive
        && player.Attributes is { } attributes
        && attributes[Stats.IsStunned] <= 0
        && attributes[Stats.IsAsleep] <= 0
        && !player.IsAtSafezone();

    /// <summary>
    /// Gets a value indicating whether the player is stunned, asleep or frozen: it can neither
    /// walk nor act until the effect ends.
    /// </summary>
    internal static bool IsCrowdControlled(Player player) =>
        player.Attributes is { } attributes
        && (attributes[Stats.IsStunned] > 0 || attributes[Stats.IsAsleep] > 0 || attributes[Stats.IsFrozen] > 0);

    /// <summary>
    /// Runs the speed-hack attack check; returns <c>true</c> when the attack comes faster than
    /// the player's attack speed allows.
    /// </summary>
    internal static async ValueTask<bool> IsTooFastAsync(Player player)
    {
        if (player.GameContext.PlugInManager.GetPlugInPoint<ISpeedHackCheatCheckPlugIn>() is not { } speedCheck)
        {
            return false;
        }

        var eventArgs = new SpeedHackCheckEventArgs();
        await speedCheck.AttackCheatCheckAsync(player, eventArgs).ConfigureAwait(false);
        return eventArgs.IsCheatDetected;
    }
}
