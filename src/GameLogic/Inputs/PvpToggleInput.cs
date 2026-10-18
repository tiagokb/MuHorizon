// <copyright file="PvpToggleInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Processed by the tick loop: turns the player's PvP on or off (see <see cref="PvpRules"/>).
/// Turning it on is immediate; turning it off needs <see cref="PvpRules.DisableDelay"/> without PvP combat.
/// </summary>
public sealed class PvpToggleInput : IPlayerInput
{
    private readonly bool _enable;

    /// <summary>
    /// Initializes a new instance of the <see cref="PvpToggleInput"/> class.
    /// </summary>
    /// <param name="enable">Whether to turn PvP on.</param>
    public PvpToggleInput(bool enable) => _enable = enable;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.PvpEnabled == _enable)
        {
            return;
        }

        if (!_enable)
        {
            var sinceCombat = DateTime.UtcNow - player.LastPvpCombatUtc;
            if (sinceCombat < PvpRules.DisableDelay)
            {
                var wait = (int)Math.Ceiling((PvpRules.DisableDelay - sinceCombat).TotalSeconds);
                await player.ShowBlueMessageAsync($"You were in PvP combat. PvP can be turned off in {wait} s.").ConfigureAwait(false);
                return;
            }
        }

        player.PvpEnabled = _enable;
        await player.ForEachWorldObserverAsync<IPvpStateChangedPlugIn>(p => p.PvpStateChangedAsync(player), true).ConfigureAwait(false);
    }
}
