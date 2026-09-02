// <copyright file="ISafezoneChangedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs the client whether the player
/// is currently standing inside a safe zone (no PvP, no attack-move UI).
/// </summary>
public interface ISafezoneChangedPlugIn : IViewPlugIn
{
    /// <summary>
    /// Called when the player's safe-zone status changes, or once on world entry to send
    /// the initial state.
    /// </summary>
    /// <param name="isInSafezone">True if the current tile is a safe zone.</param>
    ValueTask SafezoneChangedAsync(bool isInSafezone);
}
