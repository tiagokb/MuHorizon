// <copyright file="ISnapshotPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Sends a position snapshot for all entities that moved during the current tick.
/// Called once per tick by <see cref="GameTickLoop"/> after the movement phase.
/// </summary>
public interface ISnapshotPlugIn : IViewPlugIn
{
    /// <summary>
    /// Sends the snapshot to the client.
    /// </summary>
    /// <param name="positions">Moved entities: (id, continuousX, continuousY).</param>
    ValueTask SendSnapshotAsync(IReadOnlyList<(uint Id, float X, float Y)> positions);
}
