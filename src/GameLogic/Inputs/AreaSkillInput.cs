// <copyright file="AreaSkillInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Processed by the tick loop: casts an area or directional skill aimed at a ground point.
/// The point must be within the skill's range; the facing used by cone-shaped skills is
/// derived from the player's position towards it.
/// </summary>
public sealed class AreaSkillInput : IPlayerInput
{
    // AreaSkillAttackAction's marker for "no explicit target".
    private const ushort NoExtraTarget = 0xFFFF;

    private static readonly AreaSkillAttackAction Action = new();

    private readonly ushort _skillId;
    private readonly Vector2F _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="AreaSkillInput"/> class.
    /// </summary>
    /// <param name="skillId">The skill number.</param>
    /// <param name="target">The aimed ground point, in continuous server units.</param>
    public AreaSkillInput(ushort skillId, Vector2F target)
    {
        _skillId = skillId;
        _target = target;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(Player player) => this.HandleAsync(player, true);

    private async ValueTask HandleAsync(Player player, bool startCastTime)
    {
        if (player.ActiveCast is not null)
        {
            player.Logger.LogDebug("[COMBAT] AreaSkill id={SkillId}: already casting", _skillId);
            return;
        }

        if (player.CurrentMap is null || !SkillCastRules.CanAttack(player))
        {
            player.Logger.LogDebug("[COMBAT] AreaSkill id={SkillId}: player can't attack (safezone, stun or sleep)", _skillId);
            return;
        }

        if (SkillCastRules.GetCastableSkill(player, _skillId)?.Skill is not { } skill)
        {
            player.Logger.LogDebug("[COMBAT] AreaSkill id={SkillId}: skill not learned", _skillId);
            return;
        }

        var center = _target.ToTile();
        if (!player.IsInRange(center, skill.Range + SkillCastRules.RangeTolerance))
        {
            player.Logger.LogDebug("[COMBAT] AreaSkill id={SkillId}: {Center} out of range from {Position}", _skillId, center, player.Position);
            return;
        }

        if (startCastTime && await CastController.TryStartCastTimeAsync(player, skill, p => this.HandleAsync(p, false)).ConfigureAwait(false))
        {
            return;
        }

        var rotation = (byte)(player.Position.GetAngleDegreeTo(center) / 360.0 * 255.0);

        // Our client never reports explicit hits, so explicit-hit area skills hit implicitly too.
        // Ownership, mana and the speed-hack check are done by the action.
        await Action.AttackAsync(player, NoExtraTarget, _skillId, center, rotation, hitImplicitlyForExplicitSkill: true).ConfigureAwait(false);
    }
}
