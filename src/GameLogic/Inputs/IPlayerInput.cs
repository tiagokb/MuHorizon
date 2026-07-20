// <copyright file="IPlayerInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

/// <summary>
/// A player input command enqueued for processing by the game tick loop.
/// </summary>
public interface IPlayerInput
{
    /// <summary>
    /// Applies this input to <paramref name="player"/> during the current tick.
    /// </summary>
    ValueTask HandleAsync(Player player);
}
