// <copyright file="ProtobufLevelUpPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufLevelUpPlugIn : IUpdateLevelPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufLevelUpPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask UpdateLevelAsync()      => SendAsync();
    public ValueTask UpdateMasterLevelAsync() => SendAsync();

    private ValueTask SendAsync()
    {
        var attrs = _player.Attributes!;
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            LevelUp = new S2CLevelUp
            {
                Level          = (uint)attrs[Stats.Level],
                LevelUpPoints  = (uint)Math.Max(0, _player.SelectedCharacter!.LevelUpPoints),
                MaxHp          = (uint)attrs[Stats.MaximumHealth],
                MaxMp          = (uint)attrs[Stats.MaximumMana],
                MaxSd          = (uint)attrs[Stats.MaximumShield],
                MaxAg          = (uint)attrs[Stats.MaximumAbility],
            },
        });
    }
}
