// <copyright file="InvestTalentInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Processed by the tick loop: invests a talent point (<see cref="TalentRules"/>). Only out of
/// combat, like changing the skill loadout; the client always gets the trees back.
/// </summary>
public sealed class InvestTalentInput : IPlayerInput
{
    private static readonly AddMasterPointAction Action = new();

    private readonly ushort _skillNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvestTalentInput"/> class.
    /// </summary>
    /// <param name="skillNumber">The number of the talent (master skill).</param>
    public InvestTalentInput(ushort skillNumber) => _skillNumber = skillNumber;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        string? error;
        if (player.IsInCombat)
        {
            error = "You can't change talents in combat.";
        }
        else if (TalentRules.GetTalents(player).FirstOrDefault(t => t.Number == _skillNumber) is not { } talent)
        {
            error = "Your class can't learn this talent.";
        }
        else
        {
            error = TalentRules.WhyNotInvest(player, talent);
            if (error is null)
            {
                await Action.AddMasterPointAsync(player, _skillNumber).ConfigureAwait(false);
            }
        }

        await player.InvokeViewPlugInAsync<ITalentsViewPlugIn>(p => p.ShowTalentsAsync(error)).ConfigureAwait(false);
    }
}
