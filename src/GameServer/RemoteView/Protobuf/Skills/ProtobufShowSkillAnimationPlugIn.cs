// <copyright file="ProtobufShowSkillAnimationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufShowSkillAnimationPlugIn : IShowSkillAnimationPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowSkillAnimationPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowSkillAnimationAsync(IAttacker attacker, IAttackable? target, Skill skill, bool effectApplied)
        => ShowSkillAnimationAsync(attacker, target, skill.Number, effectApplied);

    public ValueTask ShowSkillAnimationAsync(IAttacker attacker, IAttackable? target, short skillNumber, bool effectApplied)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SkillAnimation = new S2CSkillAnimation
            {
                AttackerId = GetId(attacker),
                TargetId   = GetId(target),
                SkillId    = (uint)(skillNumber < 0 ? 0 : skillNumber),
                EffectApplied = effectApplied,
            },
        });

    public ValueTask ShowComboAnimationAsync(IAttacker attacker, IAttackable? target)
        => ShowSkillAnimationAsync(attacker, target, (short)59, false);

    public ValueTask ShowNovaStartAsync(IAttacker attacker)
        => ValueTask.CompletedTask;

    private static uint GetId(object? obj) => obj is ILocateable l ? l.Id : 0u;
}
