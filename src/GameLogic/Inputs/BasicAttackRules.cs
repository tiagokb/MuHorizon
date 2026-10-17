// <copyright file="BasicAttackRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Inputs;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The weapon which determines the basic attack's range and animation.
/// </summary>
public enum AttackWeapon
{
    /// <summary>Fists or melee weapons.</summary>
    Melee,

    /// <summary>Spears.</summary>
    Spear,

    /// <summary>Bows.</summary>
    Bow,

    /// <summary>Crossbows.</summary>
    Crossbow,
}

/// <summary>
/// The basic attack's range, which depends on the equipped weapon. The legacy client decided it
/// (ZzzInterface.cpp, Action: 1.8 tiles melee, 2.2 with a spear, 6 with a bow or crossbow); here the
/// server enforces it and tells the client (S2CAttackInterval).
/// </summary>
public static class BasicAttackRules
{
    private const byte SpearGroup = 3;

    /// <summary>
    /// Gets the weapon of the player's basic attack.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The weapon.</returns>
    public static AttackWeapon GetAttackWeapon(Player player)
    {
        if (player.Attributes is { } attributes)
        {
            if (attributes[Stats.IsBowEquipped] > 0)
            {
                return AttackWeapon.Bow;
            }

            if (attributes[Stats.IsCrossBowEquipped] > 0)
            {
                return AttackWeapon.Crossbow;
            }
        }

        return player.Inventory?.GetItem(InventoryConstants.LeftHandSlot)?.Definition?.Group == SpearGroup
               || player.Inventory?.GetItem(InventoryConstants.RightHandSlot)?.Definition?.Group == SpearGroup
            ? AttackWeapon.Spear
            : AttackWeapon.Melee;
    }

    /// <summary>
    /// Gets the basic attack range in whole tiles: 2 melee, 3 with a spear (one more than melee,
    /// as in the legacy client) and 6 with a bow or crossbow.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The range in tiles.</returns>
    public static int GetAttackRange(Player player) => GetAttackWeapon(player) switch
    {
        AttackWeapon.Bow or AttackWeapon.Crossbow => 6,
        AttackWeapon.Spear => 3,
        _ => 2,
    };
}
