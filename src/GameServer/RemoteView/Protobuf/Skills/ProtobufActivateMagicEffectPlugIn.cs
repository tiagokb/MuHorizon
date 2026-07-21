// <copyright file="ProtobufActivateMagicEffectPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufActivateMagicEffectPlugIn : IActivateMagicEffectPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufActivateMagicEffectPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ActivateMagicEffectAsync(MagicEffect effect, IAttackable affectedObject)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            BuffApplied = new S2CBuffApplied
            {
                TargetId   = affectedObject is ILocateable l ? l.Id : 0u,
                EffectId   = (uint)(ushort)effect.Id,
                DurationMs = (uint)effect.Duration.TotalMilliseconds,
            },
        });
}
