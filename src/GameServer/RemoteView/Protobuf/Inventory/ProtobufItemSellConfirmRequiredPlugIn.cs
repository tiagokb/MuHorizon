// <copyright file="ProtobufItemSellConfirmRequiredPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemSellConfirmRequiredPlugIn : IItemSellConfirmRequiredPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemSellConfirmRequiredPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemSellConfirmRequiredAsync(Item item, long sellPrice) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SellConfirmRequired = new S2CSellConfirmRequired
            {
                Slot      = item.ItemSlot,
                ItemName  = item.Definition?.Name ?? string.Empty,
                SellPrice = (ulong)sellPrice,
            },
        });
}