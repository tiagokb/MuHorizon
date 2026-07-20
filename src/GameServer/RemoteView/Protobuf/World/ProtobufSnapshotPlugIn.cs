// <copyright file="ProtobufSnapshotPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufSnapshotPlugIn : ISnapshotPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufSnapshotPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask SendSnapshotAsync(IReadOnlyList<(uint Id, float X, float Y)> positions)
    {
        var snap = new S2CSnapshot();
        foreach (var (id, x, y) in positions)
        {
            snap.Entities.Add(new EntitySnapshot { Id = id, X = x, Y = y });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { Snapshot = snap });
    }
}
