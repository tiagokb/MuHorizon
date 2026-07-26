// <copyright file="ProtobufProjectileHitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufProjectileHitPlugIn : IProjectileHitPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufProjectileHitPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ProjectileHitAsync(ushort projectileId, uint targetId)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ProjectileHit = new S2CProjectileHit { ProjectileId = projectileId, TargetId = targetId },
        });
    }
}
