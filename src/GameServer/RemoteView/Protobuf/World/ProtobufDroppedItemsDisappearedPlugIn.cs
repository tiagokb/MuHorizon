// <copyright file="ProtobufDroppedItemsDisappearedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufDroppedItemsDisappearedPlugIn : IDroppedItemsDisappearedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufDroppedItemsDisappearedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask DroppedItemsDisappearedAsync(IEnumerable<ushort> disappearedItemIds)
    {
        var msg = new S2CEntitiesOutOfScope();
        msg.EntityIds.AddRange(disappearedItemIds.Select(id => (uint)id));
        _player.Logger.LogInformation("[Phase5] Drop disappeared — ids=[{Ids}]", string.Join(",", disappearedItemIds));
        return _player.SendEnvelopeAsync(new ServerEnvelope { EntitiesOutOfScope = msg });
    }
}
