// <copyright file="ProtobufShowMoneyDropPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.Pathfinding;

internal sealed class ProtobufShowMoneyDropPlugIn : IShowMoneyDropPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowMoneyDropPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowMoneyAsync(ushort itemId, bool isFreshDrop, uint amount, Point point)
    {
        var pos = Vector2F.FromTile(point);
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ItemDropped = new S2CItemDropped
            {
                DropId   = itemId,
                X        = pos.X,
                Y        = pos.Y,
                IsMoney  = true,
                Amount   = amount,
            },
        });
    }
}
