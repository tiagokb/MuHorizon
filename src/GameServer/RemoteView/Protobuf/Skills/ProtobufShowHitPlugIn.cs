// <copyright file="ProtobufShowHitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufShowHitPlugIn : IShowHitPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowHitPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowHitAsync(IAttackable hitReceiver, HitInfo hitInfo)
    {
        var remainingHp = hitReceiver switch
        {
            AttackableNpcBase npc => (uint)Math.Max(0f, npc.Attributes[Stats.CurrentHealth]),
            Player p              => (uint)Math.Max(0f, p.Attributes?[Stats.CurrentHealth] ?? 0f),
            _                    => 0u,
        };

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            DamageResult = new S2CDamageResult
            {
                TargetId        = hitReceiver is ILocateable l ? l.Id : 0u,
                HealthDamage    = hitInfo.HealthDamage,
                ShieldDamage    = hitInfo.ShieldDamage,
                Attributes      = (uint)hitInfo.Attributes,
                RemainingHealth = remainingHp,
            },
        });
    }
}
