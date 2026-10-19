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

            var (shape, radius, width) = GetArea(entry.Skill);
            msg.Skills.Add(new SkillData
            {
                Slot       = slot++,
                Number     = (uint)entry.Skill.Number,
                Level      = entry.Skill.MasterDefinition is not null ? (uint)entry.Level : 0u,
                Range      = (uint)entry.Skill.Range,
                AreaShape  = shape,
                AreaRadius = radius,
                AreaWidth  = width,
                CooldownMs = (uint)Math.Max(0, entry.Skill.CooldownMilliseconds),
                CastType   = (CastType)entry.Skill.CastType,
                CastTimeMs = (uint)Math.Max(0, entry.Skill.CastTimeMilliseconds),
                Name       = entry.Skill.Name.ToString() ?? string.Empty,
            });
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { SkillList = msg }).ConfigureAwait(false);
    }

    // Mirrors how AreaSkillAttackAction picks targets: a cone (frustum) from the caster, a circle
    // of TargetAreaDiameter around the point, or everything within EffectRange (else Range) of it.
    // Explicit-target area skills (Drain Life, Chain Lightning) are aimed at an entity: no area.
    private static (SkillArea Shape, float Radius, float Width) GetArea(Skill skill)
    {
        if (skill.SkillType is not (SkillType.AreaSkillAutomaticHits or SkillType.AreaSkillExplicitHits))
            return (SkillArea.None, 0f, 0f);

        var settings = skill.AreaSkillSettings;
        if (settings is { UseFrustumFilter: true })
            return (SkillArea.Cone, settings.FrustumDistance, settings.FrustumEndWidth);
        if (settings is { UseTargetAreaFilter: true })
            return (SkillArea.Circle, settings.TargetAreaDiameter / 2f, 0f);
        return (SkillArea.Circle, settings?.EffectRange > 0 ? settings.EffectRange : skill.Range, 0f);
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
