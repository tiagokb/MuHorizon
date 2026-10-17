// <copyright file="ProtobufMapChangePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Sends <see cref="S2CMapChanged"/> after a warp: the player is off the map until the client
/// answers with <see cref="C2SMapLoaded"/>, which calls <see cref="GameLogic.Player.ClientReadyAfterMapChangeAsync"/>.
/// </summary>
internal sealed class ProtobufMapChangePlugIn : IMapChangePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufMapChangePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask MapChangeAsync()
    {
        if (_player.SelectedCharacter is not { CurrentMap: { } map } character)
        {
            return ValueTask.CompletedTask;
        }

        var position = Vector2F.FromTile(new Point(character.PositionX, character.PositionY));
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            MapChanged = new S2CMapChanged { MapId = (uint)map.Number, X = position.X, Y = position.Y },
        });
    }

    // The player stays where it is; the reason was already sent as a message.
    public ValueTask MapChangeFailedAsync() => ValueTask.CompletedTask;
}
