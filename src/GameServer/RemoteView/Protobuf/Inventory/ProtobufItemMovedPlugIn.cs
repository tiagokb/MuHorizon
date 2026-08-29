// <copyright file="ProtobufItemMovedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;

// Sends a full inventory refresh after any item move — simplest correct response
// given IItemMovedPlugIn doesn't expose the original (from) slot.
internal sealed class ProtobufItemMovedPlugIn : IItemMovedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemMovedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemMovedAsync(Item item, byte toSlot, Storages storage)
    {
        var plugin = new ProtobufUpdateInventoryListPlugIn(_player);
        return plugin.UpdateInventoryListAsync();
    }
}
