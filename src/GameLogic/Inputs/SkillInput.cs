// <copyright file="SkillInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Processed by the tick loop: resolves the target, enforces server-side cooldown and
/// line-of-sight, then dispatches to the appropriate <see cref="ITargetedSkillPlugin"/>.
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
    public async ValueTask HandleAsync(Player player)
    {
        var map = player.CurrentMap;
        if (map is null)
        {
            return;
        }

        if (map.GetObject(_targetId) is not IAttackable target)
        {
            return;
        }

        var skillEntry = player.SkillList?.GetSkill(_skillId);

        // Line-of-sight check for ranged skills (range > 1 tile).
        if (skillEntry?.Skill is { Range: > 1 })
        {
            if (!LineOfSight.HasLineOfSight(player.Position, target.Position, map))
            {
                return;
            }
        }

        // Fall back to the default attack plugin (key=0) when no specific plugin is registered.
        var plugin = player.GameContext.PlugInManager.GetStrategy<short, ITargetedSkillPlugin>((short)_skillId)
                     ?? player.GameContext.PlugInManager.GetStrategy<short, ITargetedSkillPlugin>(0);
        if (plugin is null)
        {
            return;
        }

        // Fall back to global config when the player doesn't own the skill (e.g. early test accounts).
        var skill = skillEntry?.Skill
            ?? player.GameContext.Configuration?.Skills.FirstOrDefault(s => s.Number == (short)_skillId);
        if (skill is { HasProjectile: true })
        {
            await SpawnProjectileAsync(player, target, plugin, skill).ConfigureAwait(false);
            return;
        }

        await plugin.PerformSkillAsync(player, target, _skillId).ConfigureAwait(false);
    }

    private async ValueTask SpawnProjectileAsync(Player player, IAttackable target, ITargetedSkillPlugin plugin, Skill skill)
    {
        var map = player.CurrentMap!;
        var origin = player.ContinuousPosition;
        var targetPos = Vector2F.FromTile(target.Position);
        var speed = skill.ProjectileSpeed > 0f ? skill.ProjectileSpeed : 160f;

        // Max range = skill range in tiles converted to continuous units.
        var maxRange = skill.Range > 0 ? skill.Range * 8f : 160f;

        var projectile = new Projectile(player, plugin, _skillId,
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
