// <copyright file="RavenIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

using MUnique.OpenMU.GameLogic.Pet;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// The Dark Lord's raven as a companion in the world: moves and picks targets like a summon
/// (<see cref="CompanionMode"/>), but hits with the owner's raven stats (pet level, raven damage),
/// as the legacy raven did.
/// </summary>
public sealed class RavenIntelligence : SummonedMonsterIntelligence
{
    private readonly AttackerSurrogate _attacker;

    /// <summary>
    /// Initializes a new instance of the <see cref="RavenIntelligence"/> class.
    /// </summary>
    /// <param name="owner">The owner.</param>
    public RavenIntelligence(Player owner)
        : base(owner)
    {
        this._attacker = new AttackerSurrogate(owner, new RavenAttributeSystem(owner));
    }

    /// <inheritdoc />
    protected override async ValueTask AttackTargetAsync(IAttackable target)
    {
        await target.AttackByAsync(this._attacker, null, false).ConfigureAwait(false);
        await this.Monster.ForEachWorldObserverAsync<IShowAnimationPlugIn>(
            p => p.ShowMonsterAttackAnimationAsync(this.Monster, target, this.Monster.GetDirectionTo(target)),
            true).ConfigureAwait(false);
    }
}
