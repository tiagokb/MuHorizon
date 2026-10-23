// <copyright file="CompanionMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// How a companion (a summoned monster) behaves; the owner changes it with a command, not a skill.
/// </summary>
public enum CompanionMode
{
    /// <summary>
    /// Follows the owner and doesn't attack.
    /// </summary>
    Follow = 0,

    /// <summary>
    /// Attacks what the owner attacks.
    /// </summary>
    AssistOwner = 1,

    /// <summary>
    /// Attacks the monsters around the owner.
    /// </summary>
    Aggressive = 2,

    /// <summary>
    /// Attacks who attacks the owner (or the companion).
    /// </summary>
    Defend = 3,

    /// <summary>
    /// Holds its position and only attacks what comes into its attack range.
    /// </summary>
    Stay = 4,
}
