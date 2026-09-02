// <copyright file="ProtobufTeleportPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;
using MUnique.OpenMU.Pathfinding;

// ShowTeleportedAsync fires while player.Position == target (before map.MoveAsync restores it).
// We send a one-shot S2CSnapshot so the client sees the new position immediately,
// and update ContinuousPosition so subsequent tick snapshots stay consistent.
internal sealed class ProtobufTeleportPlugIn : ITeleportPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufTeleportPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowTeleportedAsync()
    {
        var tile = _player.Position;
        var wx = tile.X * 8f + 4f;
        var wy = tile.Y * 8f + 4f;
        _player.ContinuousPosition = new Vector2F(wx, wy);
        var snap = new S2CSnapshot();
        snap.Entities.Add(new EntitySnapshot { Id = (uint)_player.Id, X = wx, Y = wy });
        return _player.SendEnvelopeAsync(new ServerEnvelope { Snapshot = snap });
    }
}