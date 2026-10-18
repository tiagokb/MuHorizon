// <copyright file="ProtobufItemMovedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Vault;

// Sends a full inventory refresh after any item move — simplest correct response
// given IItemMovedPlugIn doesn't expose the original (from) slot.
internal sealed class ProtobufItemMovedPlugIn : IItemMovedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemMovedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ItemMovedAsync(Item item, byte toSlot, Storages storage)
    {
        await new ProtobufUpdateInventoryListPlugIn(_player).UpdateInventoryListAsync().ConfigureAwait(false);

        if (_player.Vault is not null)
            await new ProtobufShowVaultPlugIn(_player).ShowVaultAsync().ConfigureAwait(false);

        await NPC.ProtobufChaosMachine.SendAsync(_player).ConfigureAwait(false);

        // Own items put on (or taken off) the trade table.
        if (_player.ViewPlugIns.GetPlugIn<GameLogic.Views.Trade.IChangeTradeButtonStatePlugIn>() is Trading.ProtobufTradePlugIn trade)
        {
            await trade.SendStateAsync().ConfigureAwait(false);
        }
    }
}
