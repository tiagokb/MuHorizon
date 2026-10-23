// <copyright file="CompanionCommandInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>
/// Processed by the tick loop: changes the mode of the own companion, or dismisses it (its skill
/// cooldown starts then).
/// </summary>
public sealed class CompanionCommandInput : IPlayerInput
{
    private readonly CompanionMode _mode;
    private readonly bool _dismiss;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionCommandInput"/> class.
    /// </summary>
    /// <param name="mode">The new mode.</param>
    /// <param name="dismiss">Whether to dismiss the companion instead.</param>
    public CompanionCommandInput(CompanionMode mode, bool dismiss)
    {
        _mode = mode;
        _dismiss = dismiss;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        if (player.PetCompanion is ({ IsAlive: true } raven, _))
        {
            // The raven stays while it's equipped: no dismiss, only modes.
            player.SetPetCompanionMode(_mode);
            await player.InvokeViewPlugInAsync<ICompanionViewPlugIn>(p => p.ShowCompanionAsync(raven, _mode)).ConfigureAwait(false);
            return;
        }

        if (player.Summon is not ({ IsAlive: true } companion, SummonedMonsterIntelligence intelligence))
        {
            await player.InvokeViewPlugInAsync<ICompanionViewPlugIn>(p => p.ShowCompanionAsync(null, CompanionMode.Aggressive)).ConfigureAwait(false);
            return;
        }

        if (_dismiss)
        {
            await player.RemoveSummonAsync().ConfigureAwait(false);
            return;
        }

        intelligence.SetMode(_mode);
        await player.InvokeViewPlugInAsync<ICompanionViewPlugIn>(p => p.ShowCompanionAsync(companion, _mode)).ConfigureAwait(false);
    }
}
