// <copyright file="SkillSlotTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The initial loadout slot types of the skills: passives are passive; buffs, heals, teleports and
/// other utilities are utility (D, F); summons and the big skills of each class are ultimate (R);
/// the rest is basic (Q, W, E). A first pass to play with - tune it in the admin panel.
/// </summary>
internal static class SkillSlotTypes
{
    private static readonly short[] Ultimates =
    [
        40, 10, 14, 13,             // Dark Wizard: Nova, Hellfire, Inferno, Cometfall
        42, 232,                    // Dark Knight: Rageful Blow, Strike of Destruction
        30, 31, 32, 33, 34, 35, 36, // Elf: summons
        236, 237,                   // Magic Gladiator: Flame Strike, Gigantic Storm
        62, 65, 238,                // Dark Lord: Earthshake, Electric Spike, Chaotic Diseier
        224, 225,                   // Summoner: Requiem, Pollution
        263,                        // Rage Fighter: Dark Side
    ];

    // Crowd control which is no buff, but plays like a spell: Stun.
    private static readonly short[] Utilities = [67];

    /// <summary>
    /// Sets the slot types of the skills of the game configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills)
        {
            skill.SlotType = GetSlotType(skill);
        }
    }

    private static SkillSlotType GetSlotType(Skill skill)
    {
        if (skill.SkillType == SkillType.PassiveBoost)
        {
            return SkillSlotType.Passive;
        }

        if (Ultimates.Contains(skill.Number))
        {
            return SkillSlotType.Ultimate;
        }

        if (Utilities.Contains(skill.Number)
            || skill.SkillType is SkillType.Buff or SkillType.Regeneration or SkillType.Other)
        {
            return SkillSlotType.Utility;
        }

        return SkillSlotType.Basic;
    }
}
