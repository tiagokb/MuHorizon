// <copyright file="IEnteredWorldPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation notifies the client that the character
/// has successfully entered the game world (map assigned, ID allocated, AoI active).
/// </summary>
public interface IEnteredWorldPlugIn : IViewPlugIn
{
    /// <summary>
    /// Notifies the client that the character entered the world.
    /// </summary>
    ValueTask EnteredWorldAsync();
}
