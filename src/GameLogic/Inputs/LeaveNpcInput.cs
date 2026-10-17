// <copyright file="LeaveNpcInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Enqueued by the tick loop when a player walks out of range of the NPC whose window is open
/// (chaos machine, vault, shop): closes it. The chaos machine returns its items to the inventory
/// first, and stays open with a message when they don't fit.
/// </summary>
public sealed class LeaveNpcInput : IPlayerInput
{
    /// <summary>
    /// The distance in tiles (per axis) beyond which an NPC window closes; a bit more than the
    /// range in which the NPC can be talked to.
    /// </summary>
    public const int LeaveRangeTiles = 6;

    private static readonly CloseNpcDialogAction Action = new();

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.OpenedNpc is not { } npc)
        {
            return;
        }

        if (player.PlayerState.CurrentState == PlayerState.NpcDialogOpened)
        {
            await Action.CloseNpcDialogAsync(player).ConfigureAwait(false);
            return;
        }

        // Shops don't keep the dialog state (the player already walks around in it).
        player.OpenedNpc = null;
        await player.InvokeViewPlugInAsync<INpcDialogClosedPlugIn>(p => p.DialogClosedAsync(npc.Definition)).ConfigureAwait(false);
    }
}
