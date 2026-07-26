// <copyright file="IProjectileHitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about a projectile hitting a target.
/// </summary>
public interface IProjectileHitPlugIn : IViewPlugIn
{
    /// <summary>
    /// Notifies the client that a projectile hit a target.
    /// </summary>
    /// <param name="projectileId">The projectile identifier.</param>
    /// <param name="targetId">The target entity identifier.</param>
    ValueTask ProjectileHitAsync(ushort projectileId, uint targetId);
}
