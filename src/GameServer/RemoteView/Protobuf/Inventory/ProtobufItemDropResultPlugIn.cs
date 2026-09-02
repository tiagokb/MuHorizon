// <copyright file="ProtobufItemDropResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.GameLogic.Views.Inventory;

// On success the item was already removed from inventory server-side; send a full inventory refresh.
// On failure nothing changed; send nothing.
internal sealed class ProtobufItemDropResultPlugIn : IItemDropResultPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemDropResultPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemDropResultAsync(byte slot, bool success) =>
        success
            ? new ProtobufUpdateInventoryListPlugIn(_player).UpdateInventoryListAsync()
            : ValueTask.CompletedTask;
}