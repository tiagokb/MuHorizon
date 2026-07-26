// <copyright file="ProtobufShowDroppedItemsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufShowDroppedItemsPlugIn : IShowDroppedItemsPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowDroppedItemsPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ShowDroppedItemsAsync(IEnumerable<DroppedItem> droppedItems, bool freshDrops)
    {
        foreach (var drop in droppedItems)
        {
            var pos = Vector2F.FromTile(drop.Position);
            var def = drop.Item.Definition;
            _player.Logger.LogInformation("[Phase5] Item drop — id={DropId} item={ItemName} at ({X:F0},{Y:F0}) freshDrop={Fresh}",
                drop.Id, def?.Name.ToString() ?? "?", pos.X, pos.Y, freshDrops);

            await _player.SendEnvelopeAsync(new ServerEnvelope
            {
                ItemDropped = new S2CItemDropped
                {
                    DropId     = drop.Id,
                    X          = pos.X,
                    Y          = pos.Y,
                    ItemNumber = (uint)(def?.Number ?? 0),
                    ItemGroup  = (uint)(def?.Group ?? 0),
                    IsMoney    = false,
                    Amount     = 1,
                },
            }).ConfigureAwait(false);
        }
    }
}
