// <copyright file="CastController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Runs the <see cref="Player.ActiveCast"/>: starts charges and cast times, advances them each
/// tick, releases, completes, cancels and interrupts them.
/// <list type="bullet">
/// <item><see cref="SkillCastType.Charge"/>: the player can walk (slowed); the damage grows from
/// <see cref="ChargeMinimumDamageFactor"/> to full over the cast time. A full charge is held for
/// <see cref="ChargeHoldAtMaximum"/>, then released automatically.</item>
/// <item><see cref="SkillCastType.CastTime"/>: the player stands still; walking cancels the cast.</item>
/// </list>
/// Crowd control (stun, sleep), death, safezones and leaving the map interrupt both. A cancelled
/// or interrupted cast has no effect, but the skill goes on cooldown.
/// </summary>
internal static class CastController
{
    /// <summary>
    /// The damage factor of a charge skill released immediately.
    /// </summary>
    internal const float ChargeMinimumDamageFactor = 0.3f;

    /// <summary>
    /// The movement speed factor while charging.
    /// </summary>
    internal const float ChargeMovementSpeedFactor = 0.6f;

    /// <summary>
    /// How long a full charge is held before it's released automatically.
    /// </summary>
    internal static readonly TimeSpan ChargeHoldAtMaximum = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Starts the cast time of a <see cref="SkillCastType.CastTime"/> skill instead of performing it.
    /// Called by the skill inputs once the cast passed their checks.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="skill">The skill.</param>
    /// <param name="complete">Performs the skill once the cast time passed.</param>
    /// <returns><c>true</c>, if the skill has a cast time, so the caller must not perform it now.</returns>
    internal static async ValueTask<bool> TryStartCastTimeAsync(Player player, Skill skill, Func<Player, ValueTask> complete)
    {
        if (skill.CastType != SkillCastType.CastTime || skill.CastTimeMilliseconds <= 0)
        {
            return false;
        }

        await StartAsync(player, skill, complete).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Starts charging a <see cref="SkillCastType.Charge"/> skill.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="skillId">The skill number.</param>
    internal static async ValueTask StartChargeAsync(Player player, ushort skillId)
    {
        if (player.ActiveCast is not null || player.CurrentMap is null || !SkillCastRules.CanAttack(player))
        {
            return;
        }

        if (SkillCastRules.GetCastableSkill(player, skillId)?.Skill is not { CastType: SkillCastType.Charge, CastTimeMilliseconds: > 0 } skill)
        {
            player.Logger.LogDebug("[COMBAT] Charge id={SkillId}: not a learned charge skill", skillId);
            return;
        }

        await StartAsync(player, skill, null).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the charged skill.
    /// </summary>
    /// <param name="player">The player.</param>
    internal static async ValueTask ReleaseChargeAsync(Player player)
    {
        if (player.ActiveCast is not { Complete: null } cast)
        {
            return;
        }

        var charge = Math.Clamp((DateTime.UtcNow - cast.StartedAt) / cast.Duration, 0, 1);
        var damageFactor = ChargeMinimumDamageFactor + ((1 - ChargeMinimumDamageFactor) * (float)charge);
        await EndAsync(player, cast, true).ConfigureAwait(false);
        await ChargeRelease.PerformAsync(player, cast.Skill, damageFactor).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the cast or charge: no effect, but the skill goes on cooldown.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="reason">The reason, for the log.</param>
    internal static async ValueTask CancelAsync(Player player, string reason)
    {
        if (player.ActiveCast is not { } cast)
        {
            return;
        }

        player.Logger.LogDebug("[COMBAT] Cast {Skill} cancelled: {Reason}", cast.Skill.Number, reason);
        await EndAsync(player, cast, false).ConfigureAwait(false);
        await player.StartCooldownAsync(cast.Skill).ConfigureAwait(false);
    }

    /// <summary>
    /// Advances the player's cast; called by the tick loop after the player's inputs.
    /// </summary>
    /// <param name="player">The player.</param>
    internal static async ValueTask UpdateAsync(Player player)
    {
        if (player.ActiveCast is not { } cast)
        {
            return;
        }

        if (!player.IsAlive || player.CurrentMap is null || !SkillCastRules.CanAttack(player))
        {
            await CancelAsync(player, "interrupted").ConfigureAwait(false);
            return;
        }

        var elapsed = DateTime.UtcNow - cast.StartedAt;
        if (cast.Complete is { } complete)
        {
            if (elapsed >= cast.Duration)
            {
                await EndAsync(player, cast, true).ConfigureAwait(false);
                await complete(player).ConfigureAwait(false);
            }
        }
        else if (elapsed >= cast.Duration + ChargeHoldAtMaximum)
        {
            await ReleaseChargeAsync(player).ConfigureAwait(false);
        }
    }

    private static async ValueTask StartAsync(Player player, Skill skill, Func<Player, ValueTask>? complete)
    {
        if (await player.IsOnCooldownAsync(skill).ConfigureAwait(false))
        {
            return;
        }

        var cast = new ActiveCast(skill, TimeSpan.FromMilliseconds(skill.CastTimeMilliseconds), complete);
        if (complete is null)
        {
            cast.Slow = new ConstantElement(ChargeMovementSpeedFactor, AggregateType.Multiplicate);
            player.Attributes!.AddElement(cast.Slow, Stats.MovementSpeedFactor);
        }
        else
        {
            player.ContinuousWalkTarget = null; // casting roots the player
        }

        player.ActiveCast = cast;
        player.Logger.LogDebug("[COMBAT] Cast {Skill} started: {Type} {Duration} ms", skill.Number, skill.CastType, skill.CastTimeMilliseconds);
        var hold = complete is null ? ChargeHoldAtMaximum : TimeSpan.Zero;
        await player.ForEachWorldObserverAsync<IShowCastPlugIn>(p => p.ShowCastStartedAsync(player, skill.Number, skill.CastType, cast.Duration, hold), true).ConfigureAwait(false);
    }

    private static async ValueTask EndAsync(Player player, ActiveCast cast, bool completed)
    {
        player.ActiveCast = null;
        if (cast.Slow is { } slow)
        {
            player.Attributes?.RemoveElement(slow, Stats.MovementSpeedFactor);
        }

        await player.ForEachWorldObserverAsync<IShowCastPlugIn>(p => p.ShowCastEndedAsync(player, cast.Skill.Number, completed), true).ConfigureAwait(false);
    }
}
