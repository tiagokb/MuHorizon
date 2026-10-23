// <copyright file="SummonedMonsterIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The companion controller of a summoned monster: it follows its owner and picks targets by its
/// <see cref="CompanionMode"/>, which the owner changes with a command (never a skill).
/// </summary>
public class SummonedMonsterIntelligence : BasicMonsterIntelligence
{
    /// <summary>
    /// How far (tiles) from the owner the companion looks for monsters in <see cref="CompanionMode.Aggressive"/>.
    /// </summary>
    private const int AggressiveRange = 8;

    /// <summary>
    /// How long the owner's last target or last attacker stays relevant.
    /// </summary>
    private static readonly TimeSpan MemoryTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Initializes a new instance of the <see cref="SummonedMonsterIntelligence"/> class.
    /// </summary>
    /// <param name="owner">The owner.</param>
    public SummonedMonsterIntelligence(Player owner)
    {
        this.Owner = owner;

        // Summons should be allowed to walk within safezones to follow their owner.
        this.CanWalkOnSafezone = true;
    }

    /// <summary>
    /// Gets the owner of the summoned monster.
    /// </summary>
    public Player Owner { get; }

    /// <summary>
    /// Gets the current mode; change it with <see cref="SetMode"/>.
    /// </summary>
    public CompanionMode Mode { get; private set; } = CompanionMode.Aggressive;

    /// <inheritdoc />
    protected override bool ChasesTargets => this.Mode != CompanionMode.Stay;

    /// <summary>
    /// Changes the mode; the current target is dropped.
    /// </summary>
    /// <param name="mode">The new mode.</param>
    public void SetMode(CompanionMode mode)
    {
        this.Mode = mode;
        this.ClearTarget();
    }

    /// <inheritdoc />
    public override void RegisterHit(IAttacker attacker)
    {
        if (attacker == this.Owner || this.Mode == CompanionMode.Follow)
        {
            // Never attack the owner; following means not fighting back either.
            return;
        }

        if (this.CurrentTarget is null
            || attacker.IsInRange(this.Npc.Position, this.Npc.Definition.AttackRange))
        {
            base.RegisterHit(attacker);
        }
    }

    /// <inheritdoc />
    protected override ValueTask<IAttackable?> SearchNextTargetAsync()
    {
        var target = this.Mode switch
        {
            CompanionMode.AssistOwner => Recent(this.Owner.LastHitTarget, this.Owner.LastHitTargetUtc),
            CompanionMode.Defend => Recent(this.Owner.LastAttacker, this.Owner.LastAttackerUtc),
            CompanionMode.Aggressive => this.NearestMonster(this.Owner.Position, AggressiveRange),
            CompanionMode.Stay => this.NearestMonster(this.Npc.Position, this.Npc.Definition.AttackRange),
            _ => null,
        };

        return ValueTask.FromResult(target);
    }

    /// <inheritdoc />
    protected override async ValueTask<bool> CanAttackAsync()
    {
        // Too far from the owner (chasing, pushed, left behind): back to the owner at once.
        var leashRange = this.Owner.GameContext.Configuration.CompanionLeashRange;
        if (this.Owner.IsAlive && leashRange > 0 && this.Owner.GetDistanceTo(this.Npc) > leashRange)
        {
            this.ClearTarget();
            await this.Monster.MoveAsync(this.Monster.CurrentMap!.Terrain.GetRandomCoordinate(this.Owner.Position, 1)).ConfigureAwait(false);
            return false;
        }

        if (this.Mode == CompanionMode.Stay)
        {
            return true;
        }

        if (this.Mode == CompanionMode.Follow)
        {
            this.ClearTarget();
        }
        else if (this.Mode == CompanionMode.AssistOwner
                 && Recent(this.Owner.LastHitTarget, this.Owner.LastHitTargetUtc) is { } ownerTarget
                 && ownerTarget != this.CurrentTarget)
        {
            // The owner switched targets: switch too.
            this.ClearTarget();
        }

        var maxDistance = this.CurrentTarget is null ? 2 : 5;
        var distanceToOwner = this.Owner.GetDistanceTo(this.Npc);
        if (distanceToOwner > maxDistance && this.Owner.IsAlive)
        {
            // follow the player
            var target = this.Monster.CurrentMap!.Terrain.GetRandomCoordinate(this.Owner.Position, 2);
            if (this.Monster.Observers.Contains(this.Owner))
            {
                await this.Monster.WalkToAsync(target).ConfigureAwait(false);
            }
            else
            {
                await this.Monster.MoveAsync(target).ConfigureAwait(false);
            }

            return false;
        }

        return this.Mode != CompanionMode.Follow;
    }

    /// <inheritdoc />
    protected override ValueTask TickWithoutTargetAsync() => ValueTask.CompletedTask; // stays with the owner

    /// <inheritdoc />
    protected override ValueTask OnNothingToDoAsync() => ValueTask.CompletedTask; // no wandering off

    private static IAttackable? Recent(IAttackable? target, DateTime atUtc) =>
        target is { IsAlive: true } && DateTime.UtcNow - atUtc < MemoryTime && !target.IsAtSafezone()
            ? target
            : null;

    private IAttackable? NearestMonster(Point center, int range) =>
        this.Owner.CurrentMap?.GetAttackablesInRange(center, range)
            .Where(o => o is Monster { SummonedBy: null, IsAlive: true })
            .OfType<NonPlayerCharacter>()
            .OrderBy(o => o.GetDistanceTo(this.Npc))
            .FirstOrDefault() as IAttackable;
}
