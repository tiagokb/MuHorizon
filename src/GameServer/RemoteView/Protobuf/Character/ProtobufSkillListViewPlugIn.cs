// <copyright file="ProtobufSkillListViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using System.Linq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// The learned skills (S2CSkillList) and the equipped ones (S2CSkillLoadout), which are sent with it.
/// </summary>
internal sealed class ProtobufSkillListViewPlugIn : ISkillListViewPlugIn, ISkillLoadoutPlugIn
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
                SlotType   = (Network.Protobuf.SkillSlotType)entry.Skill.SlotType,
                Targeting  = GetTargeting(entry.Skill),
            });
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { SkillList = msg }).ConfigureAwait(false);
        await this.ShowLoadoutAsync(SkillLoadout.Get(_player), null).ConfigureAwait(false);
    }

    public ValueTask ShowLoadoutAsync(IReadOnlyList<ushort> loadout, string? error)
    {
        var msg = new S2CSkillLoadout { Error = error ?? string.Empty };
        foreach (var number in loadout)
        {
            msg.Skills.Add(number);
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { SkillLoadout = msg });
    }

    // Implicit targets (party, players around, the caster) need no aiming; a restriction to the
    // party or players makes it an ally skill; Summon (party) and Teleport Ally are "other" skills.
    private static SkillTargeting GetTargeting(Skill skill)
    {
        if (skill.Target is SkillTarget.ImplicitParty or SkillTarget.ImplicitPlayer or SkillTarget.ImplicitPlayersInRange
                or SkillTarget.ImplicitAllInRange or SkillTarget.ImplicitNpcsInRange
            || skill.TargetRestriction == SkillTargetRestriction.Self)
            return SkillTargeting.Self;
        if (skill.TargetRestriction is SkillTargetRestriction.Party or SkillTargetRestriction.Player)
            return SkillTargeting.Ally;
        if (skill.SkillType == SkillType.Other)
            return skill.Range == 0 ? SkillTargeting.Self : SkillTargeting.Ally;
        return SkillTargeting.Enemy;
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
