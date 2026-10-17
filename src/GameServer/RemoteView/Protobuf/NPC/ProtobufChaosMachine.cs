// <copyright file="ProtobufChaosMachine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Builds <see cref="S2CChaosMachine"/>: the items in the chaos machine and the mix they'd make.
/// </summary>
internal static class ProtobufChaosMachine
{
    /// <summary>
    /// The crafting action; its handlers are cached, so it's shared.
    /// </summary>
    internal static readonly ItemCraftAction CraftAction = new();

    /// <summary>
    /// Sends the chaos machine's state, if the player has it open.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="opened"><c>true</c>, if the player just opened it: only then the client opens its window.</param>
    internal static ValueTask SendAsync(ProtobufRemotePlayer player, bool opened = false)
    {
        if (player.OpenedNpc?.Definition.NpcWindow != NpcWindow.ChaosMachine || player.TemporaryStorage is not { } machine)
        {
            return ValueTask.CompletedTask;
        }

        var msg = new S2CChaosMachine { Opened = opened };
        foreach (var item in machine.Items.Where(i => i.Definition is not null))
        {
            msg.Items.Add(ProtobufUpdateInventoryListPlugIn.BuildItemData(item));
        }

        if (CraftAction.GetMixPreview(player) is { } preview)
        {
            msg.MixName = preview.Crafting.Name.ToString();
            msg.SuccessRate = preview.SuccessRate;
            msg.Price = (uint)Math.Max(0, preview.Price);
        }

        return player.SendEnvelopeAsync(new ServerEnvelope { ChaosMachine = msg });
    }
}
