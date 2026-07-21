// <copyright file="SkillInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.PlugIns;

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

        await plugin.PerformSkillAsync(player, target, _skillId).ConfigureAwait(false);
    }
}
