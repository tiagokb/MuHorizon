// <copyright file="MapPvpModes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The initial PvP modes of the maps: towns are PvE only, every other map is "flagged"
/// (players who turned PvP on can fight). Maps can be changed in the admin panel.
/// </summary>
internal static class MapPvpModes
{
    // Lorencia, Devias, Noria, Elbeland.
    private static readonly short[] Towns = [0, 2, 3, 51];

    /// <summary>
    /// Sets the PvP modes of the maps of the game configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(GameConfiguration gameConfiguration)
    {
        foreach (var map in gameConfiguration.Maps)
        {
            map.PvpMode = Towns.Contains(map.Number) ? MapPvpMode.Disabled : MapPvpMode.Flagged;
        }
    }
}
