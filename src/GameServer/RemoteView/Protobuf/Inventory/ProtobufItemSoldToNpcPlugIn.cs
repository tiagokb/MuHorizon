// <copyright file="ProtobufItemSoldToNpcPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.GameLogic.Views.Inventory;

internal sealed class ProtobufItemSoldToNpcPlugIn : IItemSoldToNpcPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufItemSoldToNpcPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ItemSoldToNpcAsync(bool success)
    {
        if (!success) return;
        await new ProtobufUpdateInventoryListPlugIn(_player).UpdateInventoryListAsync().ConfigureAwait(false);
    }
}