// <copyright file="ProtobufProjectileSpawnedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufProjectileSpawnedPlugIn : IProjectileSpawnedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufProjectileSpawnedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ProjectileSpawnedAsync(ushort projectileId, uint ownerId, ushort skillId,
        float originX, float originY, float velocityX, float velocityY)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ProjectileSpawned = new S2CProjectileSpawned
            {
                ProjectileId = projectileId,
                OwnerId = ownerId,
                SkillId = skillId,
                OriginX = originX,
                OriginY = originY,
                VelocityX = velocityX,
                VelocityY = velocityY,
            },
        });
    }
}
