// <copyright file="SkillCastType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Defines how a skill is cast.
/// </summary>
public enum SkillCastType
{
    /// <summary>
    /// The skill takes effect as soon as it's cast.
    /// </summary>
    Instant,

    /// <summary>
    /// The skill is charged while its key is held and released by the player: the longer the
    /// charge (up to <see cref="Skill.CastTimeMilliseconds"/>), the stronger the effect.
    /// </summary>
    Charge,

    /// <summary>
    /// The skill takes effect after casting for <see cref="Skill.CastTimeMilliseconds"/>.
    /// Cancelling the cast has no effect, but still puts the skill on cooldown.
    /// </summary>
    CastTime,
}
