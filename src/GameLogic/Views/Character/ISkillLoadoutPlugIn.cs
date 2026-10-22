// <copyright file="ISkillLoadoutPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Shows the equipped skills (<see cref="SkillLoadout"/>).
/// </summary>
public interface ISkillLoadoutPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the loadout.
    /// </summary>
    /// <param name="loadout">The skill numbers per slot (Q, W, E, R, D, F); 0 is an empty slot.</param>
    /// <param name="error">Why a requested change was refused, or <c>null</c>.</param>
    ValueTask ShowLoadoutAsync(IReadOnlyList<ushort> loadout, string? error);
}
