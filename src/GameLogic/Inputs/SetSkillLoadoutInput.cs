// <copyright file="SetSkillLoadoutInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.Views.Character;

/// <summary>
/// Processed by the tick loop: changes the equipped skills (<see cref="SkillLoadout"/>). Refused in
/// combat (<see cref="Player.IsInCombat"/>) or when invalid; the client always gets the loadout back.
/// </summary>
public sealed class SetSkillLoadoutInput : IPlayerInput
{
    private readonly ushort[] _loadout;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSkillLoadoutInput"/> class.
    /// </summary>
    /// <param name="loadout">The skill numbers per slot (Q, W, E, R, D, F).</param>
    public SetSkillLoadoutInput(ushort[] loadout) => _loadout = loadout;

    /// <inheritdoc />
    public async ValueTask HandleAsync(Player player)
    {
        string? error;
        if (player.IsInCombat)
        {
            error = "You can't change skills in combat.";
        }
        else
        {
            error = SkillLoadout.Validate(player, _loadout);
            if (error is null)
            {
                SkillLoadout.Set(player, _loadout);
            }
        }

        var loadout = SkillLoadout.Get(player);
        await player.InvokeViewPlugInAsync<ISkillLoadoutPlugIn>(p => p.ShowLoadoutAsync(loadout, error)).ConfigureAwait(false);
    }
}
