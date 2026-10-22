// <copyright file="SkillSlotType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// The loadout slot a skill can be equipped in: the character knows many skills, but only equips
/// three basic (Q, W, E), one ultimate (R) and two utility (D, F) skills; passives are always on.
/// </summary>
public enum SkillSlotType
{
    /// <summary>
    /// A basic skill (Q, W, E).
    /// </summary>
    Basic = 0,

    /// <summary>
    /// An ultimate skill (R).
    /// </summary>
    Ultimate = 1,

    /// <summary>
    /// A utility skill or spell (D, F): buffs, teleports, crowd control.
    /// </summary>
    Utility = 2,

    /// <summary>
    /// A passive skill: never equipped, always active.
    /// </summary>
    Passive = 3,
}
