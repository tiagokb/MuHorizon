// <copyright file="ProtobufAttackInterval.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CAttackInterval"/>: the minimum attack interval the speed-hack check
/// accepts, so the client paces attacks and casts by the server's rule instead of its own
/// formula (attacks faster than this are dropped and count as speed-hack warnings).
/// </summary>
internal static class ProtobufAttackInterval
{
    internal static async ValueTask SendAsync(ProtobufRemotePlayer player)
    {
        if (player.GameContext.PlugInManager.GetPlugInPoint<ISpeedHackCheatCheckPlugIn>() is not { } speedCheck)
        {
            return; // no check active: the client keeps its own pacing
        }

        var eventArgs = new AttackIntervalEventArgs();
        await speedCheck.GetMinimumAttackIntervalAsync(player, eventArgs).ConfigureAwait(false);
        if (eventArgs.MinimumInterval is not { } interval)
        {
            return;
        }

        await player.SendEnvelopeAsync(new ServerEnvelope
        {
            AttackInterval = new S2CAttackInterval { MinIntervalMs = (uint)Math.Ceiling(interval.TotalMilliseconds) },
        }).ConfigureAwait(false);
    }
}
