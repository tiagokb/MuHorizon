// <copyright file="ProtobufItemDurabilityChangedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemDurabilityChangedPlugIn : IItemDurabilityChangedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemDurabilityChangedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemDurabilityChangedAsync(Item item, bool causedByConsumption)
    {
        if (item.Definition is null) return default;

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ItemAppeared = new S2CItemAppeared
            {
                Item = ProtobufUpdateInventoryListPlugIn.BuildItemData(item),
            },
        });
    }
}