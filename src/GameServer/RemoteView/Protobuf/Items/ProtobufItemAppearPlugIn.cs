// <copyright file="ProtobufItemAppearPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemAppearPlugIn : IItemAppearPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemAppearPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemAppearAsync(Item newItem)
    {
        _player.Logger.LogInformation("[Phase5] Pickup success — item={ItemName}", newItem.Definition?.Name.ToString() ?? "?");
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PickUpResult = new S2CPickUpResult { Success = true, DropId = 0 },
        });
    }
}
