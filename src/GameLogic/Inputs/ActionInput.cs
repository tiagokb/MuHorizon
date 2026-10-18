// <copyright file="ActionInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

/// <summary>
/// Runs an action in the tick loop of the player's map, like any other input. Used by
/// server-driven players (test dummies) whose logic runs on their own timer, so their effects on
/// the map and other players happen in the same thread as everything else there.
/// </summary>
public sealed class ActionInput : IPlayerInput
{
    private readonly Func<Player, ValueTask> _action;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionInput"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    public ActionInput(Func<Player, ValueTask> action) => _action = action;

    /// <inheritdoc />
    public ValueTask HandleAsync(Player player) => _action(player);
}
