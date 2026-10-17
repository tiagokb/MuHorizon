// <copyright file="SkillCasting.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// The skills which are charged or cast over time, used by the <see cref="SkillsInitializer"/>
/// and the update which adds them to existing databases.
/// </summary>
internal static class SkillCasting
{
    /// <summary>
    /// The cooldown of Nova, which got none before it became a charge skill.
    /// </summary>
    internal const int NovaCooldownMilliseconds = 5000;

    private static readonly IReadOnlyDictionary<SkillNumber, (SkillCastType Type, int Milliseconds)> Casts = new Dictionary<SkillNumber, (SkillCastType, int)>
    {
        { SkillNumber.Nova, (SkillCastType.Charge, 2000) },
        { SkillNumber.Cometfall, (SkillCastType.CastTime, 800) },
        { SkillNumber.Inferno, (SkillCastType.CastTime, 1000) },
        { SkillNumber.Hellfire, (SkillCastType.CastTime, 600) },
    };

    /// <summary>
    /// Sets the cast types and times of the skills of the game configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills)
        {
            if (Casts.TryGetValue((SkillNumber)skill.Number, out var cast))
            {
                skill.CastType = cast.Type;
                skill.CastTimeMilliseconds = cast.Milliseconds;
            }
        }
    }
}
