// <copyright file="EventMonsterStrength.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The initial monster strength of the event maps (Devil Square, Blood Castle, Chaos Castle):
/// their monsters are much weaker than their definitions. Tunable per map in the admin panel.
/// </summary>
internal static class EventMonsterStrength
{
    /// <summary>
    /// The monster strength of the event maps, in percent.
    /// </summary>
    public const int EventMapStrengthPercent = 25;

    private static readonly short[] EventMaps = [9, 32, 11, 12, 13, 14, 15, 16, 17, 52, 18, 19, 20, 21, 22, 23, 53];

    /// <summary>
    /// Sets the monster strength of the event maps of the game configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var map in gameConfiguration.Maps)
        {
            if (EventMaps.Contains(map.Number))
            {
                map.MonsterStrengthPercent = EventMapStrengthPercent;
            }
        }
    }
}
