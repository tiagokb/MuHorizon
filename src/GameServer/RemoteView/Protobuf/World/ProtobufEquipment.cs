// <copyright file="ProtobufEquipment.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Builds the visible-equipment list of a player for <see cref="EntityState"/> and
/// <see cref="S2CAppearanceChanged"/>. Shared so both messages always agree.
/// </summary>
internal static class ProtobufEquipment
{
    // Slots 0-7 change how a character looks (weapons, armor set, wings).
    // Pet, pendant and rings (8-11) have no character model.
    private static readonly byte LastVisibleSlot = InventoryConstants.WingsSlot;

    internal static IEnumerable<EquipmentPiece> Build(Player player) =>
        (player.Inventory?.EquippedItems ?? Enumerable.Empty<Item>())
            .Where(i => i.Definition is not null && i.ItemSlot <= LastVisibleSlot)
            .Select(BuildPiece);

    private static EquipmentPiece BuildPiece(Item item) => new()
    {
        Slot      = item.ItemSlot,
        Group     = item.Definition!.Group,
        Number    = (uint)item.Definition.Number,
        Level     = item.Level,
        Excellent = item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent),
        Ancient   = item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.AncientOption),
    };
}
