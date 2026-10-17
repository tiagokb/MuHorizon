// <copyright file="MoveInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

/// <summary>
/// Sets the player's walk target when dequeued by the tick loop.
/// Coordinates are in continuous server units (1 tile = 8 units).
/// </summary>
public sealed class MoveInput : IPlayerInput
{
    private readonly float _targetX;
    private readonly float _targetY;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveInput"/> class.
    /// </summary>
    public MoveInput(float targetX, float targetY)
    {
        _targetX = targetX;
        _targetY = targetY;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        // Casting roots the player: walking cancels it. Charging allows walking (slowed).
        if (player.ActiveCast is { Complete: not null })
        {
            await CastController.CancelAsync(player, "moved").ConfigureAwait(false);
        }

        player.ContinuousWalkTarget = new Vector2F(_targetX, _targetY);
    }
}
