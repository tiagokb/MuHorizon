// <copyright file="ProtobufEnteredWorldPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufEnteredWorldPlugIn : IEnteredWorldPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufEnteredWorldPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask EnteredWorldAsync()
    {
        var map = _player.CurrentMap ?? throw new InvalidOperationException("Player has no current map.");
        var pos = _player.ContinuousPosition;

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EnteredWorld = new S2CEnteredWorld
            {
                MapId = map.MapId,
                X = pos.X,
                Y = pos.Y,
                OwnId = _player.Id,
            },
        });
    }
}
