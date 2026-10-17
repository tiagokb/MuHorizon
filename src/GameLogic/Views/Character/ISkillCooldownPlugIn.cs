// <copyright file="ISkillCooldownPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Interface of a view whose implementation informs the player about the cooldown of one of its skills.
/// </summary>
public interface ISkillCooldownPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows that the skill can't be cast again for the remaining time: sent when a cast puts it
    /// on cooldown, and when a cast is rejected because it's still cooling down.
    /// </summary>
    /// <param name="skillNumber">The skill number.</param>
    /// <param name="remaining">The remaining cooldown.</param>
    ValueTask ShowSkillCooldownAsync(short skillNumber, TimeSpan remaining);
}
