// <copyright file="AttackIntervalEventArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Event args for <see cref="ISpeedHackCheatCheckPlugIn.GetMinimumAttackIntervalAsync"/>.
/// </summary>
public class AttackIntervalEventArgs
{
    /// <summary>
    /// Gets or sets the minimum interval between attacks; <c>null</c> when no check is active.
    /// </summary>
    public TimeSpan? MinimumInterval { get; set; }
}
