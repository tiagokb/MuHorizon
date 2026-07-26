// <copyright file="IProjectileSpawnedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about a newly spawned projectile.
/// </summary>
public interface IProjectileSpawnedPlugIn : IViewPlugIn
{
    /// <summary>
    /// Notifies the client that a projectile was spawned.
    /// </summary>
    /// <param name="projectileId">The projectile identifier.</param>
    /// <param name="ownerId">The entity that fired the projectile.</param>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="originX">X position in continuous units where the projectile starts.</param>
    /// <param name="originY">Y position in continuous units where the projectile starts.</param>
    /// <param name="velocityX">X component of the velocity (units/sec).</param>
    /// <param name="velocityY">Y component of the velocity (units/sec).</param>
    ValueTask ProjectileSpawnedAsync(ushort projectileId, uint ownerId, ushort skillId,
        float originX, float originY, float velocityX, float velocityY);
}
