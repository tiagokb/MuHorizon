// <copyright file="ProtobufAttackInterval.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CAttackInterval"/>: the minimum attack interval the speed-hack check
/// accepts, so the client paces attacks and casts by the server's rule instead of its own
/// formula (attacks faster than this are dropped and count as speed-hack warnings).
/// </summary>
internal static class ProtobufAttackInterval
{
    /// <summary>Attributes whose change can change the attack range or weapon.</summary>
    internal static readonly HashSet<AttributeDefinition> WeaponInputs =
    [
        Stats.IsBowEquipped, Stats.IsCrossBowEquipped, Stats.EquippedWeaponCount,
    ];

    internal static async ValueTask SendAsync(ProtobufRemotePlayer player)
    {
        // Without an active speed-hack check the interval is unknown (0): the client keeps its own pacing.
        var minIntervalMs = 0u;
        if (player.GameContext.PlugInManager.GetPlugInPoint<ISpeedHackCheatCheckPlugIn>() is { } speedCheck)
        {
            var eventArgs = new AttackIntervalEventArgs();
            await speedCheck.GetMinimumAttackIntervalAsync(player, eventArgs).ConfigureAwait(false);
            if (eventArgs.MinimumInterval is { } interval)
            {
                minIntervalMs = (uint)Math.Ceiling(interval.TotalMilliseconds);
            }
        }

        await player.SendEnvelopeAsync(new ServerEnvelope
        {
            AttackInterval = new S2CAttackInterval
            {
                MinIntervalMs = minIntervalMs,
                AttackRange   = (uint)GameLogic.Inputs.BasicAttackRules.GetAttackRange(player),
                Weapon        = (AttackWeapon)GameLogic.Inputs.BasicAttackRules.GetAttackWeapon(player),
            },
        }).ConfigureAwait(false);
    }
}
