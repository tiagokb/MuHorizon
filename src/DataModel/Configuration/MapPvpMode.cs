// <copyright file="MapPvpMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Whether players can fight each other on a map. Safezones never allow combat, and
/// members of the same party never hurt each other.
/// </summary>
public enum MapPvpMode
{
    /// <summary>
    /// Players who turned PvP on can fight each other (the default).
    /// </summary>
    Flagged,

    /// <summary>
    /// No player can hurt another one (e.g. towns).
    /// </summary>
    Disabled,

    /// <summary>
    /// Every player can hurt every other one; PvP can't be turned off.
    /// </summary>
    Enabled,
}
