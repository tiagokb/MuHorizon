// <copyright file="CastControlInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

/// <summary>
/// Processed by the tick loop: starts charging a skill, releases the charge, or cancels the
/// current charge or cast (see <see cref="CastController"/>).
/// </summary>
public sealed class CastControlInput : IPlayerInput
{
    private readonly CastControlAction _action;
    private readonly ushort _skillId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastControlInput"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="skillId">The skill number; only used by <see cref="CastControlAction.StartCharge"/>.</param>
    public CastControlInput(CastControlAction action, ushort skillId)
    {
        _action = action;
        _skillId = skillId;
    }

    /// <summary>
    /// The cast control actions.
    /// </summary>
    public enum CastControlAction
    {
        /// <summary>
        /// Starts charging a skill.
        /// </summary>
        StartCharge,

        /// <summary>
        /// Releases the charged skill.
        /// </summary>
        Release,

        /// <summary>
        /// Cancels the current charge or cast.
        /// </summary>
        Cancel,
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(Player player) => _action switch
    {
        CastControlAction.StartCharge => CastController.StartChargeAsync(player, _skillId),
        CastControlAction.Release => CastController.ReleaseChargeAsync(player),
        _ => CastController.CancelAsync(player, "cancelled by the player"),
    };
}
