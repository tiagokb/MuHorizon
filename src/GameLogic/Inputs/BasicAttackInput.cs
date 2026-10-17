// <copyright file="BasicAttackInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// A no-skill melee attack sent when the client clicks a target with skill ID 0.
/// Bypasses the skill list so level-1 characters without skills can deal damage.
/// </summary>
public sealed class BasicAttackInput : IPlayerInput
{
    private readonly ushort _targetId;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAttackInput"/> class.
    /// </summary>
    public BasicAttackInput(ushort targetId)
    {
        _targetId = targetId;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.ActiveCast is not null)
        {
            return;
        }

        var map = player.CurrentMap;
        if (map is null)
        {
            return;
        }

        if (map.GetObject(_targetId) is not IAttackable target)
        {
            return;
        }

        if (!target.IsActive() || target.IsAtSafezone())
        {
            return;
        }

        if (!SkillCastRules.CanAttack(player) || !player.IsInRange(target.Position, BasicAttackRules.GetAttackRange(player) + 1))
        {
            return;
        }

        if (await SkillCastRules.IsTooFastAsync(player).ConfigureAwait(false))
        {
            return;
        }

        var targetName = target is Player tp ? tp.Name : target.GetType().Name;
        player.Logger.LogDebug("[COMBAT] BasicAttack  player={Player}  target={Target}", player.Name, targetName);

        await player.ForEachWorldObserverAsync<IShowAnimationPlugIn>(
            p => p.ShowAnimationAsync(player, 0, target, player.Rotation), true).ConfigureAwait(false);

        var hitInfo = await target.AttackByAsync(player, null, false).ConfigureAwait(false);
        if (hitInfo is null)
        {
            player.Logger.LogDebug("[COMBAT] BasicAttack  player={Player}  target={Target}  → no hitInfo (immune/safe)", player.Name, targetName);
            return;
        }

        player.LastAttackedTarget.SetTarget(target);
        await player.ForEachWorldObserverAsync<IShowHitPlugIn>(
            p => p.ShowHitAsync(target, hitInfo.Value), true).ConfigureAwait(false);
    }
}
