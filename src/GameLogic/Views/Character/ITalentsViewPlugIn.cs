// <copyright file="ITalentsViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Shows the talent trees of the player (see <see cref="TalentRules"/>).
/// </summary>
public interface ITalentsViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the talent trees with the invested levels and the free points.
    /// </summary>
    /// <param name="error">Why a requested investment was refused, or <c>null</c>.</param>
    ValueTask ShowTalentsAsync(string? error);
}
