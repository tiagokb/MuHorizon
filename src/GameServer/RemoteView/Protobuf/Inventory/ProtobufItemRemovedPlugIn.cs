// <copyright file="ProtobufItemRemovedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemRemovedPlugIn : IItemRemovedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemRemovedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask RemoveItemAsync(byte inventorySlot) =>
        _player.SendEnvelopeAsync(new ServerEnvelope { ItemRemoved = new S2CItemRemoved { Slot = inventorySlot } });
}
