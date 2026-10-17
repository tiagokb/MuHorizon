// <copyright file="SkillInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Processed by the tick loop: resolves the target, requires a learned skill and line-of-sight,
/// then spawns a projectile or dispatches to the appropriate <see cref="ITargetedSkillPlugin"/>.
/// </summary>
public sealed class SkillInput : IPlayerInput
{
    private readonly ushort _skillId;
    private readonly ushort _targetId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillInput"/> class.
    /// </summary>
    public SkillInput(ushort skillId, ushort targetId)
    {
        _skillId = skillId;
        _targetId = targetId;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(Player player) => this.HandleAsync(player, true);

    private async ValueTask HandleAsync(Player player, bool startCastTime)
    {
        if (player.ActiveCast is not null)
        {
            player.Logger.LogDebug("[COMBAT] SkillCast id={SkillId}: already casting", _skillId);
            return;
        }

        var map = player.CurrentMap;
        if (map is null)
        {
            return;
        }

        if (map.GetObject(_targetId) is not IAttackable target)
        {
            player.Logger.LogDebug("[COMBAT] SkillCast id={SkillId}: target {TargetId} not found on map", _skillId, _targetId);
            return;
        }

        // Only learned skills can be cast; the client may request any skill number.
        if (SkillCastRules.GetCastableSkill(player, _skillId) is not { Skill: { } skill } skillEntry)
        {
            player.Logger.LogDebug("[COMBAT] SkillCast id={SkillId}: skill not learned", _skillId);
            return;
        }

        // Line-of-sight check for ranged skills (range > 1 tile).
        if (skill.Range > 1)
        {
            if (!LineOfSight.HasLineOfSight(player.Position, target.Position, map))
            {
                return;
            }
        }

        // Charge skills are started and released by CastControlInput.
        if (skill.CastType == SkillCastType.Charge)
        {
            return;
        }

        if (startCastTime && await CastController.TryStartCastTimeAsync(player, skill, p => this.HandleAsync(p, false)).ConfigureAwait(false))
        {
            return;
        }

        // Fall back to the default attack plugin (key=0) when no specific plugin is registered.
        var plugin = player.GameContext.PlugInManager.GetStrategy<short, ITargetedSkillPlugin>((short)_skillId)
                     ?? player.GameContext.PlugInManager.GetStrategy<short, ITargetedSkillPlugin>(0);
        if (plugin is null)
        {
            player.Logger.LogDebug("[COMBAT] SkillCast id={SkillId}: no ITargetedSkillPlugin found", _skillId);
            return;
        }

        var targetName = target is Player tp ? tp.Name : target.GetType().Name;
        player.Logger.LogDebug(
            "[COMBAT] SkillCast  id={SkillId}  skill={Skill}  type={DmgType}  hasProjectile={Proj}  plugin={Plugin}  target={Target}",
            _skillId,
            skill.Name,
            skill.DamageType.ToString(),
            skill.HasProjectile,
            plugin.GetType().Name,
            targetName);

        if (skill.HasProjectile)
        {
            // Projectiles bypass the skill plugin, so they enforce the plugin's checks here.
            if (!SkillCastRules.CanAttack(player)
                || !target.IsActive()
                || !player.IsInRange(target.Position, skill.Range + SkillCastRules.RangeTolerance)
                || await SkillCastRules.IsTooFastAsync(player).ConfigureAwait(false)
                || !await player.TryConsumeForSkillAsync(skill).ConfigureAwait(false))
            {
                return;
            }

            await SpawnProjectileAsync(player, target, skill, skillEntry).ConfigureAwait(false);
            return;
        }

        await plugin.PerformSkillAsync(player, target, _skillId).ConfigureAwait(false);
    }

    private async ValueTask SpawnProjectileAsync(Player player, IAttackable target, Skill skill, SkillEntry skillEntry)
    {
        var map = player.CurrentMap!;
        var origin = player.ContinuousPosition;
        var targetPos = Vector2F.FromTile(target.Position);
        var speed = skill.ProjectileSpeed > 0f ? skill.ProjectileSpeed : 160f;

        // Max range = skill range in tiles converted to continuous units.
        var maxRange = skill.Range > 0 ? skill.Range * 8f : 160f;

        var projectile = new Projectile(player, skillEntry, _skillId,
            origin.X, origin.Y, targetPos.X, targetPos.Y, speed, maxRange);

        map.AddProjectile(projectile);

        var pId = projectile.Id;
        var ownerId = (uint)player.Id;
        var ox = origin.X;
        var oy = origin.Y;
        var vx = projectile.VelocityX;
        var vy = projectile.VelocityY;
        var sid = _skillId;

        foreach (var observer in map.Players)
        {
            await observer.InvokeViewPlugInAsync<IProjectileSpawnedPlugIn>(
                p => p.ProjectileSpawnedAsync(pId, ownerId, sid, ox, oy, vx, vy)).ConfigureAwait(false);
        }
    }
}
