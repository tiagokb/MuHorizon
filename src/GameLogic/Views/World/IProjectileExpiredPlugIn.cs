// <copyright file="IProjectileExpiredPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about a projectile that expired (hit a wall or timed out).
/// </summary>
public interface IProjectileExpiredPlugIn : IViewPlugIn
{
    /// <summary>
    /// Notifies the client that a projectile expired without hitting a target.
    /// </summary>
    /// <param name="projectileId">The projectile identifier.</param>
    ValueTask ProjectileExpiredAsync(ushort projectileId);
}
