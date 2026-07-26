// <copyright file="ProtobufProjectileExpiredPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufProjectileExpiredPlugIn : IProjectileExpiredPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufProjectileExpiredPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ProjectileExpiredAsync(ushort projectileId)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ProjectileExpired = new S2CProjectileExpired { ProjectileId = projectileId },
        });
    }
}
