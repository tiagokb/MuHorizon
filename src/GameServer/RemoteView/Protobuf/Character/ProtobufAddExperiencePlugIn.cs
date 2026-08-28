// <copyright file="ProtobufAddExperiencePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;
using GameXpType = MUnique.OpenMU.GameLogic.Views.Character.ExperienceType;

internal sealed class ProtobufAddExperiencePlugIn : IAddExperiencePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufAddExperiencePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask AddExperienceAsync(int gainedExperience, IAttackable? killedObject, GameXpType experienceType)
    {
        if (gainedExperience <= 0)
            return ValueTask.CompletedTask;

        var level    = (uint)(_player.Attributes?[Stats.Level] ?? 0);
        var expTable = _player.GameServerContext.ExperienceTable;
        var expNext  = level < expTable.Length - 1
            ? (ulong)expTable[level + 1]
            : ulong.MaxValue;

        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            ExperienceGained = new S2CExperienceGained
            {
                Gained    = (uint)gainedExperience,
                Total     = (ulong)(_player.SelectedCharacter?.Experience ?? 0),
                NextLevel = expNext,
                KilledId  = killedObject is ILocateable loc ? loc.Id : 0u,
                IsMaster  = experienceType == GameXpType.Master,
            },
        });
    }
}
