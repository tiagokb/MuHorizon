// <copyright file="MovementSpeedConstants.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

/// <summary>
/// Movement speed constants used by configuration initializers and update plugins.
/// </summary>
internal static class MovementSpeedConstants
{
    /// <summary>
    /// Gets the minimum level to use running gear.
    /// </summary>
    internal const int RunningGearMinimumLevel = 5;

    /// <summary>
    /// Gets the movement speed of running gear.
    /// </summary>
    internal const float RunningGearMovementSpeed = 15f;

    /// <summary>
    /// Gets the default movement speed of wings.
    /// </summary>
    internal const float DefaultWingMovementSpeed = 15f;

    /// <summary>
    /// Gets the movement speed of fast wings.
    /// </summary>
    internal const float FastWingMovementSpeed = 16f;

    /// <summary>
    /// Gets the basic movement speed of mounts.
    /// </summary>
    internal const float BasicMountMovementSpeed = 15f;

    /// <summary>
    /// Gets the movement speed of horse or fenrir.
    /// </summary>
    internal const float HorseOrFenrirMovementSpeed = 17f;

    /// <summary>
    /// Gets the movement speed of upgraded fenrir.
    /// </summary>
    internal const float UpgradedFenrirMovementSpeed = 19f;

    // ── Additive model ──────────────────────────────────────────────────────────
    // Movement speed = base + sum of item bonuses (AddRaw), then MovementSpeedFactor (slows).
    // The absolute values above are only used by the historical AddMovementSpeedAttributes updates.

    /// <summary>
    /// Gets the base movement speed of every character (3.75 tiles/sec), before item bonuses.
    /// </summary>
    internal const float BaseMovementSpeed = 15f;

    /// <summary>
    /// Gets the movement speed bonus of boots (land) and gloves (underwater), at any item level.
    /// </summary>
    internal const float RunningGearMovementSpeedBonus = 1f;

    /// <summary>
    /// Gets the movement speed bonus of first tier wings.
    /// </summary>
    internal const float FirstTierWingMovementSpeedBonus = 2f;

    /// <summary>
    /// Gets the movement speed bonus of second tier wings and capes.
    /// </summary>
    internal const float SecondTierWingMovementSpeedBonus = 3f;

    /// <summary>
    /// Gets the movement speed bonus of third tier wings and capes.
    /// </summary>
    internal const float ThirdTierWingMovementSpeedBonus = 4f;

    /// <summary>
    /// Gets the movement speed bonus of mounts (Uniria, Dinorant, Dark Horse, Fenrir).
    /// </summary>
    internal const float MountMovementSpeedBonus = 2f;

    /// <summary>
    /// Gets the additional movement speed bonus of an upgraded (black, blue or gold) Fenrir.
    /// </summary>
    internal const float UpgradedFenrirMovementSpeedBonus = 1f;

    /// <summary>
    /// Gets the movement speed factor when iced.
    /// </summary>
    internal const float IcedMovementSpeedFactor = 0.5f;

    /// <summary>
    /// Gets the movement speed factor when slowed by cold.
    /// </summary>
    internal const float ColdMovementSpeedFactor = 0.33f;

    /// <summary>
    /// Gets the movement speed bonus of a wing slot item (group 12) by its tier.
    /// </summary>
    /// <param name="wingNumber">The item number of the wing or cape.</param>
    /// <returns>The bonus; unknown wings count as first tier.</returns>
    internal static float GetWingMovementSpeedBonus(short wingNumber) => wingNumber switch
    {
        3 or 4 or 5 or 6 or 30 or 42 or 49 => SecondTierWingMovementSpeedBonus,      // Spirits, Soul, Dragon, Darkness, Cape of Lord, Despair, Cape of Fighter
        >= 36 and <= 40 or 43 or 50 => ThirdTierWingMovementSpeedBonus,              // Storm, Eternal, Illusion, Ruin, Cape of Emperor, Dimension, Cape of Overrule
        _ => FirstTierWingMovementSpeedBonus,                                        // Elf, Heaven, Satan, Curse
    };
}
