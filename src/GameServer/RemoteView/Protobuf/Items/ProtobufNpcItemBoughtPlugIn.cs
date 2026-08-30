// <copyright file="ProtobufNpcItemBoughtPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufNpcItemBoughtPlugIn : INpcItemBoughtPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufNpcItemBoughtPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask NpcItemBoughtAsync(Item newItem)
    {
        if (newItem.Definition is null) return ValueTask.CompletedTask;

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ItemAppeared = new S2CItemAppeared { Item = ProtobufUpdateInventoryListPlugIn.BuildItemData(newItem) },
        });
    }
}
