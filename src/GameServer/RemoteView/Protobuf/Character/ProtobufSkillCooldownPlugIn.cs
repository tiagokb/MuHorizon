// <copyright file="ProtobufSkillCooldownPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CSkillCooldown"/>, so the client's cooldowns follow the server's.
/// </summary>
internal sealed class ProtobufSkillCooldownPlugIn : ISkillCooldownPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufSkillCooldownPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowSkillCooldownAsync(short skillNumber, TimeSpan remaining)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SkillCooldown = new S2CSkillCooldown
            {
                SkillId = (uint)skillNumber,
                RemainingMs = (uint)Math.Max(0, Math.Ceiling(remaining.TotalMilliseconds)),
            },
        });
    }
}
