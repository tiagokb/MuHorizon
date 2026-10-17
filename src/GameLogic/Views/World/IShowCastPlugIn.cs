// <copyright file="IShowCastPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Interface of a view whose implementation shows players charging or casting a skill
/// (<see cref="SkillCastType.Charge"/> and <see cref="SkillCastType.CastTime"/>).
/// </summary>
public interface IShowCastPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows that a player started charging or casting a skill.
    /// </summary>
    /// <param name="caster">The casting player.</param>
    /// <param name="skillNumber">The skill number.</param>
    /// <param name="castType">The cast type.</param>
    /// <param name="duration">The cast time, or the time to fully charge.</param>
    /// <param name="hold">How long a full charge is held before it's released automatically; zero for cast times.</param>
    ValueTask ShowCastStartedAsync(Player caster, short skillNumber, SkillCastType castType, TimeSpan duration, TimeSpan hold);

    /// <summary>
    /// Shows that a player's cast ended.
    /// </summary>
    /// <param name="caster">The casting player.</param>
    /// <param name="skillNumber">The skill number.</param>
    /// <param name="completed"><c>true</c>, if the skill was released or completed; <c>false</c>, if it was cancelled or interrupted.</param>
    ValueTask ShowCastEndedAsync(Player caster, short skillNumber, bool completed);
}
