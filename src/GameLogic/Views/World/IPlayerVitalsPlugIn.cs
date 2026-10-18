// <copyright file="IPlayerVitalsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation shows the health and shield of another player
/// (the bar above its head).
/// </summary>
public interface IPlayerVitalsPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the player's current and maximum health and shield.
    /// </summary>
    /// <param name="player">The observed player.</param>
    ValueTask ShowVitalsAsync(Player player);
}
