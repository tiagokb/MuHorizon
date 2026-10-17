// <copyright file="EnterGateInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlayerActions;

/// <summary>
/// Enqueued by the tick loop when a player walks onto an <see cref="EnterGate"/> of its map:
/// warps the player to the gate's target, if the level and map requirements are met.
/// The legacy client detected gates itself and requested the warp; here the server does.
/// </summary>
public sealed class EnterGateInput : IPlayerInput
{
    private static readonly WarpGateAction Action = new();

    private readonly EnterGate _gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterGateInput"/> class.
    /// </summary>
    /// <param name="gate">The entered gate.</param>
    public EnterGateInput(EnterGate gate) => _gate = gate;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.CurrentMap is null || !player.IsAlive)
        {
            return;
        }

        await CastController.CancelAsync(player, "entered a gate").ConfigureAwait(false);
        player.ContinuousWalkTarget = null;
        await Action.EnterGateAsync(player, _gate).ConfigureAwait(false);
    }
}
