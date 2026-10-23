// <copyright file="ICompanionViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Shows the own companion (summoned monster) and its mode, for the companion control.
/// </summary>
public interface ICompanionViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the companion; <c>null</c> when there's none (died or dismissed).
    /// </summary>
    /// <param name="companion">The companion.</param>
    /// <param name="mode">Its mode.</param>
    ValueTask ShowCompanionAsync(Monster? companion, CompanionMode mode);
}
