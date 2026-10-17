// <copyright file="ChargeRelease.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Performs a released <see cref="SkillCastType.Charge"/> skill: a burst hitting everything
/// within <see cref="Skill.Range"/> around the caster (Nova). Replaces the legacy Nova
/// plug-ins, which charged in a background task in 12 stages of 500 ms.
/// </summary>
internal static class ChargeRelease
{
    /// <summary>
    /// The damage bonus of the last legacy Nova stage (<see cref="Stats.NovaStageDamage"/>),
    /// which the skill's damage formula still reads; the charge now scales the whole damage instead.
    /// </summary>
    private const float NovaFullStageDamage = 1320;

    /// <summary>
    /// Performs the released skill.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="skill">The skill.</param>
    /// <param name="damageFactor">The damage factor reached by the charge.</param>
    internal static async ValueTask PerformAsync(Player player, Skill skill, float damageFactor)
    {
        if (player.SkillList?.GetSkill((ushort)skill.Number) is not { } skillEntry
            || player.CurrentMap is not { } map
            || !await player.TryConsumeForSkillAsync(skill).ConfigureAwait(false))
        {
            return;
        }

        player.Logger.LogDebug("[COMBAT] Charge {Skill} released: damage x{Factor:0.00}", skill.Number, damageFactor);
        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, player, skill.Number, true), true).ConfigureAwait(false);

        var targets = map.GetAttackablesInRange(player.Position, skill.Range)
            .Where(a => a.IsAlive && a != player && IsTarget(player, a))
            .ToList();
        var stageDamage = new ConstantElement(NovaFullStageDamage);
        player.Attributes!.AddElement(stageDamage, Stats.NovaStageDamage);
        try
        {
            foreach (var target in targets)
            {
                await target.AttackByAsync(player, skillEntry, false, damageFactor).ConfigureAwait(false);
            }
        }
        finally
        {
            player.Attributes?.RemoveElement(stageDamage, Stats.NovaStageDamage);
        }
    }

    /// <summary>
    /// Same targets as the legacy Nova: monsters which aren't summoned by a player, and players
    /// only in a tied guild war.
    /// </summary>
    private static bool IsTarget(Player player, IAttackable attackable) => attackable switch
    {
        Monster { SummonedBy: { } summoner } => IsTargetPlayer(player, summoner),
        Monster => true,
        Player other => IsTargetPlayer(player, other),
        _ => false,
    };

    private static bool IsTargetPlayer(Player player, Player other) =>
        other != player
        && player.GuildWarContext is { } attackerContext
        && other.GuildWarContext is { } defenderContext
        && attackerContext.Team != defenderContext.Team
        && attackerContext.Score == defenderContext.Score;
}
