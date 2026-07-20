// <copyright file="IGameTickable.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// Represents an entity or system that must be updated every game tick.
/// </summary>
public interface IGameTickable
{
    /// <summary>
    /// Advances this object by one tick.
    /// </summary>
    /// <param name="deltaTime">Elapsed time since the last tick, clamped to 3× the target interval.</param>
    ValueTask TickAsync(TimeSpan deltaTime);
}
