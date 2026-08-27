// <copyright file="ProtobufShowAnimationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;
using Direction = MUnique.OpenMU.DataModel.Configuration.Direction;

/// <summary>
/// Protobuf implementation of <see cref="IShowAnimationPlugIn"/>.
/// Monster melee attacks (no configured AttackSkill) are broadcast via the existing
/// <see cref="S2CSkillAnimation"/> message with SkillId = 0, so the client can play
/// the attack/shock animations using the same handler as skill-based attacks.
/// </summary>
internal sealed class ProtobufShowAnimationPlugIn : IShowAnimationPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowAnimationPlugIn(ProtobufRemotePlayer player) => _player = player;

    // Player basic melee attack: broadcast the same S2CSkillAnimation shape so the client
    // can play attack on the attacker and shock on the target without a separate message.
    public ValueTask ShowAnimationAsync(IIdentifiable animatingObj, byte animation, IIdentifiable? targetObj, Direction direction)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SkillAnimation = new S2CSkillAnimation
            {
                AttackerId    = animatingObj.Id,
                TargetId      = targetObj?.Id ?? 0u,
                SkillId       = 0,
                EffectApplied = true,
            },
        });

    // Monster melee attack: reuse S2CSkillAnimation so the client plays attack on the
    // monster and shock on the target without a separate message type.
    public ValueTask ShowMonsterAttackAnimationAsync(IIdentifiable animatingObj, IIdentifiable? targetObj, Direction direction)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SkillAnimation = new S2CSkillAnimation
            {
                AttackerId    = animatingObj.Id,
                TargetId      = targetObj?.Id ?? 0u,
                SkillId       = 0,
                EffectApplied = true,
            },
        });
}
