// <copyright file="Projectile.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// A physical projectile that travels across the map until it hits a target or exceeds its range.
/// Implements <see cref="IGameTickable"/> so the tick loop advances it each frame without
/// per-entity timers (P5 compliance).
/// </summary>
public sealed class Projectile : IGameTickable
{
    private readonly Player _owner;
    private readonly ITargetedSkillPlugin _hitPlugin;
    private readonly float _speedPerSec;
    private readonly float _maxRange;
    private readonly float _dirX;
    private readonly float _dirY;
    private float _posX;
    private float _posY;
    private float _traveled;
    private bool _done;

    /// <summary>
    /// Initializes a new instance of the <see cref="Projectile"/> class.
    /// </summary>
    public Projectile(
        Player owner,
        ITargetedSkillPlugin hitPlugin,
        ushort skillId,
        float originX,
        float originY,
        float targetX,
        float targetY,
        float speedPerSec,
        float maxRange)
    {
        _owner = owner;
        _hitPlugin = hitPlugin;
        SkillId = skillId;
        Map = owner.CurrentMap!;
        OwnerId = (uint)owner.Id;
        OriginX = originX;
        OriginY = originY;
        _posX = originX;
        _posY = originY;
        _speedPerSec = speedPerSec;
        _maxRange = maxRange;

        var dx = targetX - originX;
        var dy = targetY - originY;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f)
        {
            _dirX = 1f;
            _dirY = 0f;
        }
        else
        {
            _dirX = dx / len;
            _dirY = dy / len;
        }
    }

    /// <summary>Gets the unique identifier assigned by <see cref="GameMap"/>.</summary>
    public ushort Id { get; set; }

    /// <summary>Gets the map this projectile belongs to.</summary>
    public GameMap Map { get; }

    /// <summary>Gets the entity ID of the player who fired this projectile.</summary>
    public uint OwnerId { get; }

    /// <summary>Gets the skill identifier.</summary>
    public ushort SkillId { get; }

    /// <summary>Gets the X coordinate (continuous units) where the projectile was spawned.</summary>
    public float OriginX { get; }

    /// <summary>Gets the Y coordinate (continuous units) where the projectile was spawned.</summary>
    public float OriginY { get; }

    /// <summary>Gets the X component of the velocity (units/sec) for client-side interpolation.</summary>
    public float VelocityX => _dirX * _speedPerSec;

    /// <summary>Gets the Y component of the velocity (units/sec) for client-side interpolation.</summary>
    public float VelocityY => _dirY * _speedPerSec;

    /// <inheritdoc />
    public async ValueTask TickAsync(TimeSpan deltaTime)
    {
        if (_done)
        {
            return;
        }

        var dt = (float)deltaTime.TotalSeconds;
        var step = _speedPerSec * dt;

        _posX += _dirX * step;
        _posY += _dirY * step;
        _traveled += step;

        if (_traveled >= _maxRange)
        {
            await ExpireAsync().ConfigureAwait(false);
            return;
        }

        var tile = new Vector2F(_posX, _posY).ToTile();
        var attackables = Map.GetAttackablesInRange(tile, 1);
        foreach (var target in attackables)
        {
            if ((uint)target.Id == OwnerId)
            {
                continue;
            }

            _done = true;
            Map.RemoveProjectile(this);

            await _hitPlugin.PerformSkillAsync(_owner, target, SkillId).ConfigureAwait(false);

            var capturedId = Id;
            var capturedTargetId = (uint)target.Id;
            foreach (var observer in Map.Players)
            {
                await observer.InvokeViewPlugInAsync<IProjectileHitPlugIn>(
                    p => p.ProjectileHitAsync(capturedId, capturedTargetId)).ConfigureAwait(false);
            }

            return;
        }
    }

    private async ValueTask ExpireAsync()
    {
        _done = true;
        Map.RemoveProjectile(this);

        var capturedId = Id;
        foreach (var observer in Map.Players)
        {
            await observer.InvokeViewPlugInAsync<IProjectileExpiredPlugIn>(
                p => p.ProjectileExpiredAsync(capturedId)).ConfigureAwait(false);
        }
    }
}
