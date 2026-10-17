// <copyright file="ProtobufShowCastPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Skills;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CCastStarted"/> and <see cref="S2CCastEnded"/>, so clients can show cast bars.
/// </summary>
internal sealed class ProtobufShowCastPlugIn : IShowCastPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowCastPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowCastStartedAsync(Player caster, short skillNumber, SkillCastType castType, TimeSpan duration, TimeSpan hold)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            CastStarted = new S2CCastStarted
            {
                CasterId   = caster.Id,
                SkillId    = (uint)skillNumber,
                CastType   = (CastType)castType,
                DurationMs = (uint)duration.TotalMilliseconds,
                HoldMs     = (uint)hold.TotalMilliseconds,
            },
        });
    }

    public ValueTask ShowCastEndedAsync(Player caster, short skillNumber, bool completed)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            CastEnded = new S2CCastEnded { CasterId = caster.Id, SkillId = (uint)skillNumber, Completed = completed },
        });
    }
}
