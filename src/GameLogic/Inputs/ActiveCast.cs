// <copyright file="ActiveCast.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// A skill a player is charging or casting (<see cref="Player.ActiveCast"/>).
/// </summary>
public sealed class ActiveCast
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveCast"/> class.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="duration">The cast time, or the time to fully charge.</param>
    /// <param name="complete">Performs a <see cref="SkillCastType.CastTime"/> skill once the cast time passed; <c>null</c> for charges.</param>
    internal ActiveCast(Skill skill, TimeSpan duration, Func<Player, ValueTask>? complete)
    {
        this.Skill = skill;
        this.Duration = duration;
        this.Complete = complete;
        this.StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the skill.
    /// </summary>
    public Skill Skill { get; }

    /// <summary>
    /// Gets the cast time, or the time to fully charge.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets when the cast started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Gets the action which performs a <see cref="SkillCastType.CastTime"/> skill once the cast time passed;
    /// <c>null</c> for charges.
    /// </summary>
    internal Func<Player, ValueTask>? Complete { get; }

    /// <summary>
    /// Gets or sets the movement slow applied while charging.
    /// </summary>
    internal IElement? Slow { get; set; }
}
