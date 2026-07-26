// <copyright file="ProtobufItemPickUpFailedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufItemPickUpFailedPlugIn : IItemPickUpFailedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemPickUpFailedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ItemPickUpFailedAsync(ItemPickFailReason reason)
    {
        _player.Logger.LogInformation("[Phase5] Pickup failed — reason={Reason}", reason);
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PickUpResult = new S2CPickUpResult { Success = false, DropId = 0 },
        });
    }
}
