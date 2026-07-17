// <copyright file="IPlayerInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

/// <summary>
/// Marker interface for player input commands enqueued for processing by the tick loop.
/// Concrete implementations (e.g. MoveInput, SkillInput) will be added per phase.
/// </summary>
public interface IPlayerInput
{
}
