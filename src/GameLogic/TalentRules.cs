// <copyright file="TalentRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The talent trees (the master skill trees): which talents a character's class has, and whether a
/// point can be invested in one — the same rules as <see cref="PlayerActions.Character.AddMasterPointAction"/>,
/// but with the reason, to show it to the player.
/// </summary>
public static class TalentRules
{
    /// <summary>
    /// The level a talent of the previous rank (or a required talent) needs to unlock the next ones.
    /// </summary>
    public const int RequiredTalentLevel = 10;

    /// <summary>
    /// Gets the talents of the player's class.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The talents (master skills) the class can learn.</returns>
    public static IEnumerable<Skill> GetTalents(Player player) =>
        player.SelectedCharacter?.CharacterClass is { } characterClass
            ? player.GameContext.Configuration.Skills.Where(s => s.MasterDefinition is not null && s.QualifiedCharacters.Contains(characterClass))
            : [];

    /// <summary>
    /// Gets the invested level of a talent.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="talent">The talent.</param>
    /// <returns>The level; 0 if not learned.</returns>
    public static int GetLevel(Player player, Skill talent) =>
        player.SelectedCharacter?.LearnedSkills.FirstOrDefault(l => l.Skill == talent)?.Level ?? 0;

    /// <summary>
    /// Gets why no point can be invested in the talent right now.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="talent">The talent.</param>
    /// <returns>The reason, or <c>null</c> if a point can be invested.</returns>
    public static string? WhyNotInvest(Player player, Skill talent)
    {
        if (player.SelectedCharacter is not { } character || talent.MasterDefinition is not { } definition)
        {
            return "Not a talent.";
        }

        if (character.CharacterClass?.IsMasterClass is not true)
        {
            return "Talents unlock with the third class and master levels (level 400).";
        }

        var level = GetLevel(player, talent);
        if (level >= definition.MaximumLevel)
        {
            return "Maximum level.";
        }

        var cost = level == 0 ? definition.MinimumLevel : 1;
        if (character.MasterLevelUpPoints < cost)
        {
            return cost > 1 ? $"Needs {cost} talent points to learn." : "No talent points left.";
        }

        if (level > 0)
        {
            return null;
        }

        if (definition.Rank > 1
            && !character.LearnedSkills.Any(l => l.Skill?.MasterDefinition is { } other
                && other.Root?.Id == definition.Root?.Id
                && other.Rank == definition.Rank - 1
                && l.Level >= RequiredTalentLevel))
        {
            return $"Needs a talent of rank {definition.Rank - 1} of this tree at level {RequiredTalentLevel}.";
        }

        foreach (var required in definition.RequiredMasterSkills)
        {
            var fulfilled = required.MasterDefinition is null
                ? player.SkillList?.ContainsSkill((ushort)required.Number) is true
                : character.LearnedSkills.Any(l => l.Skill == required && l.Level >= RequiredTalentLevel);
            if (!fulfilled)
            {
                return required.MasterDefinition is null
                    ? $"Needs the skill {required.Name}."
                    : $"Needs {required.Name} at level {RequiredTalentLevel}.";
            }
        }

        return null;
    }
}
