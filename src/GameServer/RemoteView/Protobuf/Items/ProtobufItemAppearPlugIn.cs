// <copyright file="ProtobufItemAppearPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemAppearPlugIn : IItemAppearPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemAppearPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ItemAppearAsync(Item newItem)
    {
        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PickUpResult = new S2CPickUpResult { Success = true, DropId = 0 },
        }).ConfigureAwait(false);

        if (newItem.Definition is null) return;

        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ItemAppeared = new S2CItemAppeared { Item = ProtobufUpdateInventoryListPlugIn.BuildItemData(newItem) },
        }).ConfigureAwait(false);
    }
}
