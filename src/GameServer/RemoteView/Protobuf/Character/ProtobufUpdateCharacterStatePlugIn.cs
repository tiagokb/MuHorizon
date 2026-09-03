// <copyright file="ProtobufUpdateCharacterStatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufUpdateCharacterStatePlugIn : IUpdateCharacterStatsPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufUpdateCharacterStatePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask UpdateCharacterStatsAsync() =>
        _player.SendEnvelopeAsync(new ServerEnvelope { CharacterState = BuildState() });

    private S2CCharacterState BuildState()
    {
        var attrs = _player.Attributes!;
        var ch    = _player.SelectedCharacter!;
        var level = (uint)attrs[Stats.Level];

        var expTable = _player.GameServerContext.ExperienceTable;
        var expNext  = level < expTable.Length - 1
            ? (ulong)expTable[level + 1]
            : ulong.MaxValue;

        return new S2CCharacterState
        {
            Level          = level,
            LevelUpPoints  = (uint)Math.Max(0, ch.LevelUpPoints),
            CurrentHp      = (uint)Math.Max(0f, attrs[Stats.CurrentHealth]),
            MaxHp          = (uint)attrs[Stats.MaximumHealth],
            CurrentMp      = (uint)Math.Max(0f, attrs[Stats.CurrentMana]),
            MaxMp          = (uint)attrs[Stats.MaximumMana],
            CurrentSd      = (uint)Math.Max(0f, attrs[Stats.CurrentShield]),
            MaxSd          = (uint)attrs[Stats.MaximumShield],
            CurrentAg      = (uint)Math.Max(0f, attrs[Stats.CurrentAbility]),
            MaxAg          = (uint)attrs[Stats.MaximumAbility],
            Experience     = (ulong)ch.Experience,
            ExperienceNext = expNext,
            Money          = (uint)Math.Max(0, _player.Money),
            Strength       = (uint)attrs[Stats.BaseStrength],
            Agility        = (uint)attrs[Stats.BaseAgility],
            Vitality       = (uint)attrs[Stats.BaseVitality],
            Energy         = (uint)attrs[Stats.BaseEnergy],
            Leadership     = (uint)attrs[Stats.BaseLeadership],
            AttackSpeed    = (uint)attrs[Stats.AttackSpeed],
            MagicSpeed     = (uint)attrs[Stats.MagicSpeed],
            MinPhysDmg     = (uint)Math.Max(0f, attrs[Stats.MinimumPhysBaseDmg]),
            MaxPhysDmg     = (uint)Math.Max(0f, attrs[Stats.MaximumPhysBaseDmg]),
            MinWizDmg      = (uint)Math.Max(0f, attrs[Stats.MinimumWizBaseDmg]),
            MaxWizDmg      = (uint)Math.Max(0f, attrs[Stats.MaximumWizBaseDmg]),
        };
    }
}
