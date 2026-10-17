// <copyright file="WarpInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.PlayerActions;

/// <summary>
/// Processed by the tick loop: warps the player to a destination of the warp list
/// (<see cref="DataModel.Configuration.GameConfiguration.WarpList"/>), like the legacy move command.
/// </summary>
public sealed class WarpInput : IPlayerInput
{
    private static readonly WarpAction Action = new();

    private readonly int _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="WarpInput"/> class.
    /// </summary>
    /// <param name="index">The <see cref="DataModel.Configuration.WarpInfo.Index"/>.</param>
    public WarpInput(int index) => _index = index;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.CurrentMap is null || !player.IsAlive || player.ActiveCast is not null)
        {
            return;
        }

        if (player.GameContext.Configuration.WarpList.FirstOrDefault(w => w.Index == _index) is not { } warpInfo)
        {
            player.Logger.LogDebug("[WARP] Unknown warp index {Index}", _index);
            return;
        }

        player.ContinuousWalkTarget = null;
        await Action.WarpToAsync(player, warpInfo).ConfigureAwait(false);
    }
}
