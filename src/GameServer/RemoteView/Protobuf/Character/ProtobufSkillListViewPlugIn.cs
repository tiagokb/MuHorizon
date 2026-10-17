// <copyright file="ProtobufSkillListViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using System.Linq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufSkillListViewPlugIn : ISkillListViewPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufSkillListViewPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask AddSkillAsync(Skill skill) => UpdateSkillListAsync();

    public ValueTask RemoveSkillAsync(Skill skill) => UpdateSkillListAsync();

    public async ValueTask UpdateSkillListAsync()
    {
        if (_player.SkillList is not { } skillList)
            return;

        var entries = skillList.Skills.ToList();

        var replacedSkills = entries
            .Select(e => e.Skill?.MasterDefinition?.ReplacedSkill)
            .Where(s => s is not null)
            .ToHashSet();

        entries.RemoveAll(e => replacedSkills.Contains(e.Skill));
        entries.RemoveAll(e => e.Skill?.SkillType == SkillType.PassiveBoost);

        var msg = new S2CSkillList();
        uint slot = 0;
        foreach (var entry in entries)
        {
            if (entry.Skill is null)
                continue;

            msg.Skills.Add(new SkillData
            {
                Slot   = slot++,
                Number = (uint)entry.Skill.Number,
                Level  = entry.Skill.MasterDefinition is not null ? (uint)entry.Level : 0u,
                Range  = (uint)entry.Skill.Range,
            });
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { SkillList = msg }).ConfigureAwait(false);
    }

    public Skill? GetSkillByIndex(byte skillIndex)
    {
        if (_player.SkillList is not { } skillList)
            return null;

        var entries = skillList.Skills
            .Where(e => e.Skill?.SkillType != SkillType.PassiveBoost)
            .ToList();

        return skillIndex < entries.Count ? entries[skillIndex].Skill : null;
    }
}
