// <copyright file="BasicAttackInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

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

        if (!player.IsInRange(target.Position, 3))
        {
            return;
        }

        await player.ForEachWorldObserverAsync<IShowAnimationPlugIn>(
            p => p.ShowAnimationAsync(player, 0, target, player.Rotation), true).ConfigureAwait(false);

        var hitInfo = await target.AttackByAsync(player, null, false).ConfigureAwait(false);
        if (hitInfo is null)
        {
            return;
        }

        player.LastAttackedTarget.SetTarget(target);
        await player.ForEachWorldObserverAsync<IShowHitPlugIn>(
            p => p.ShowHitAsync(target, hitInfo.Value), true).ConfigureAwait(false);
    }
}
