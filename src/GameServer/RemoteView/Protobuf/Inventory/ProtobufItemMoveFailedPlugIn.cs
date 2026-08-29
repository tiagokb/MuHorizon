// <copyright file="ProtobufItemMoveFailedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;

// No-op: the client is optimistic-wait (it doesn't update state until S2CInventory arrives).
// A failed move simply means no inventory refresh is sent and the UI stays unchanged.
internal sealed class ProtobufItemMoveFailedPlugIn : IItemMoveFailedPlugIn
{
    public ProtobufItemMoveFailedPlugIn(ProtobufRemotePlayer _) { }

    public ValueTask ItemMoveFailedAsync(Item? item) => ValueTask.CompletedTask;
}
