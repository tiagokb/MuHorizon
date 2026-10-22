// <copyright file="SkillLoadout.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The skills a character has equipped: three basic (Q, W, E), one ultimate (R) and two utility
/// (D, F). Only equipped skills can be cast; passives are always active. Stored in
/// <see cref="DataModel.Entities.Character.SkillLoadout"/>.
/// </summary>
public static class SkillLoadout
{
    /// <summary>
    /// The number of slots: Q, W, E, R, D, F.
    /// </summary>
    public const int SlotCount = 6;

    /// <summary>
    /// Gets the skill type a slot accepts.
    /// </summary>
    /// <param name="slot">The slot (0 = Q … 5 = F).</param>
    /// <returns>The accepted skill slot type.</returns>
    public static SkillSlotType SlotTypeOf(int slot) => slot switch
    {
        < 3 => SkillSlotType.Basic,
        3 => SkillSlotType.Ultimate,
        _ => SkillSlotType.Utility,
    };

    /// <summary>
    /// Gets the equipped skills of the player. A character without a loadout gets a default one
    /// from its learned skills. Skills no longer learned (or of the wrong type) read as empty slots.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The skill numbers per slot; 0 is an empty slot.</returns>
    public static ushort[] Get(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return new ushort[SlotCount];
        }

        if (character.SkillLoadout is not { Length: SlotCount * 2 } stored)
        {
            var defaults = CreateDefault(player);
            character.SkillLoadout = Encode(defaults);
            return defaults;
        }

        var loadout = new ushort[SlotCount];
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var number = (ushort)(stored[slot * 2] | (stored[(slot * 2) + 1] << 8));
            loadout[slot] = Fits(player, number, slot) ? number : (ushort)0;
        }

        return loadout;
    }

    /// <summary>
    /// Determines whether the skill is equipped. The basic attack (0) always is.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="skillNumber">The skill number.</param>
    /// <returns><c>true</c> if the skill can be cast.</returns>
    public static bool IsEquipped(Player player, ushort skillNumber) =>
        skillNumber == 0 || Array.IndexOf(Get(player), skillNumber) >= 0;

    /// <summary>
    /// Validates a loadout: every skill learned, in a slot of its type, and none twice.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="loadout">The skill numbers per slot.</param>
    /// <returns>The reason it is invalid, or <c>null</c> if it is valid.</returns>
    public static string? Validate(Player player, IReadOnlyList<ushort> loadout)
    {
        if (loadout.Count != SlotCount)
        {
            return "Invalid loadout.";
        }

        for (var slot = 0; slot < SlotCount; slot++)
        {
            var number = loadout[slot];
            if (number == 0)
            {
                continue;
            }

            if (player.SkillList?.GetSkill(number)?.Skill is not { } skill)
            {
                return "You don't know this skill.";
            }

            if (skill.SlotType != SlotTypeOf(slot))
            {
                return $"{skill.Name} is a {skill.SlotType} skill and can't go in this slot.";
            }

            for (var other = 0; other < slot; other++)
            {
                if (loadout[other] == number)
                {
                    return $"{skill.Name} is already equipped.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Stores the loadout; call <see cref="Validate"/> first.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="loadout">The skill numbers per slot.</param>
    public static void Set(Player player, IReadOnlyList<ushort> loadout)
    {
        if (player.SelectedCharacter is { } character)
        {
            character.SkillLoadout = Encode(loadout);
        }
    }

    private static bool Fits(Player player, ushort number, int slot) =>
        number == 0 || player.SkillList?.GetSkill(number)?.Skill?.SlotType == SlotTypeOf(slot);

    // The first learned skills of each type, in the order they were learned.
    private static ushort[] CreateDefault(Player player)
    {
        var loadout = new ushort[SlotCount];
        var learned = player.SkillList?.Skills
            .Where(entry => entry.Skill is not null)
            .Select(entry => entry.Skill!)
            .ToList() ?? [];
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var type = SlotTypeOf(slot);
            var skill = learned.FirstOrDefault(s => s.SlotType == type && Array.IndexOf(loadout, (ushort)s.Number) < 0);
            loadout[slot] = (ushort)(skill?.Number ?? 0);
        }

        return loadout;
    }

    private static byte[] Encode(IReadOnlyList<ushort> loadout)
    {
        var bytes = new byte[SlotCount * 2];
        for (var slot = 0; slot < SlotCount && slot < loadout.Count; slot++)
        {
            bytes[slot * 2] = (byte)loadout[slot];
            bytes[(slot * 2) + 1] = (byte)(loadout[slot] >> 8);
        }

        return bytes;
    }
}
