// <copyright file="MountSlot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The mount slot (<see cref="InventoryConstants.MountSlot"/>): the "Mount" item slot type and the
/// mounts which go there (Uniria, Dinorant, Dark Horse, Fenrir), so they don't share the pet slot.
/// </summary>
internal static class MountSlot
{
    // Group 13: Horn of Uniria, Horn of Dinorant, Dark Horse, Horn of Fenrir.
    private static readonly short[] Mounts = [2, 3, 4, 37];

    /// <summary>
    /// Adds the mount slot type and puts the mounts in it.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var slotType = gameConfiguration.ItemSlotTypes.FirstOrDefault(t => t.ItemSlots.Contains(InventoryConstants.MountSlot));
        if (slotType is null)
        {
            slotType = context.CreateNew<ItemSlotType>();
            slotType.SetGuid(InventoryConstants.MountSlot);
            slotType.Description = "Mount";
            slotType.ItemSlots.Add(InventoryConstants.MountSlot);
            gameConfiguration.ItemSlotTypes.Add(slotType);
        }

        foreach (var item in gameConfiguration.Items.Where(i => i.Group == 13 && Mounts.Contains(i.Number)))
        {
            item.ItemSlot = slotType;
        }
    }
}
