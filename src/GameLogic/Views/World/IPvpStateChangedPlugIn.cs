// <copyright file="IPvpStateChangedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation shows that a player turned PvP on or off.
/// </summary>
public interface IPvpStateChangedPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the player's <see cref="Player.PvpEnabled"/>.
    /// </summary>
    /// <param name="player">The player.</param>
    ValueTask PvpStateChangedAsync(Player player);
}
