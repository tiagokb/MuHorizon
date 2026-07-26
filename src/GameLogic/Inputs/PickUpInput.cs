// <copyright file="PickUpInput.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.GameLogic.PlayerActions.Items;

/// <summary>
/// Processed by the tick loop: attempts to pick up a dropped item by its map ID.
/// </summary>
public sealed class PickUpInput : IPlayerInput
{
    private static readonly PickupItemAction Action = new();

    private readonly ushort _dropId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PickUpInput"/> class.
    /// </summary>
    public PickUpInput(ushort dropId) => _dropId = dropId;

    /// <inheritdoc />
    public ValueTask HandleAsync(Player player) => Action.PickupItemAsync(player, _dropId);
}
