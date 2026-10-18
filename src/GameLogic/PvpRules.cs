// <copyright file="PvpRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Pet;

/// <summary>
/// Who can hurt whom between players. Replaces the legacy player-killer rules:
/// <list type="bullet">
/// <item>Safezones never allow combat, and party members never hurt each other.</item>
/// <item>The map decides (<see cref="GameMapDefinition.PvpMode"/>): towns are PvE only, "flagged" maps
/// let players fight when both turned PvP on (<see cref="Player.PvpEnabled"/>), PvP maps let everyone fight.</item>
/// <item>Duels and mini games which allow killing keep their own rules.</item>
/// </list>
/// </summary>
public static class PvpRules
{
    /// <summary>
    /// How long after the last PvP hit (given or taken) PvP can't be turned off, so it can't be
    /// used to start a fight and escape.
    /// </summary>
    public static readonly TimeSpan DisableDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the player behind an attacker (the player itself, its pet or its summon).
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <returns>The player; <c>null</c> for monsters.</returns>
    public static Player? GetAttackingPlayer(IAttacker attacker) => attacker switch
    {
        Player player => player,
        AttackerSurrogate surrogate => surrogate.Owner,
        Monster { SummonedBy: { } owner } => owner,
        _ => null,
    };

    /// <summary>
    /// Determines whether the attacker can hurt the defender.
    /// </summary>
    /// <param name="attacker">The attacking player.</param>
    /// <param name="defender">The attacked player.</param>
    /// <returns><c>true</c>, if the attack is allowed.</returns>
    public static bool CanAttack(Player attacker, Player defender)
    {
        if (attacker == defender || attacker.IsAtSafezone() || defender.IsAtSafezone())
        {
            return false;
        }

        if (attacker.Party is { } party && party == defender.Party)
        {
            return false;
        }

        if ((attacker.DuelRoom is { } duel && duel == defender.DuelRoom)
            || defender.CurrentMiniGame?.AllowPlayerKilling is true)
        {
            return true;
        }

        return (defender.CurrentMap?.Definition.PvpMode ?? MapPvpMode.Disabled) switch
        {
            MapPvpMode.Enabled => true,
            MapPvpMode.Flagged => attacker.PvpEnabled && defender.PvpEnabled,
            _ => false,
        };
    }

    /// <summary>
    /// Remembers a PvP hit for both players (see <see cref="DisableDelay"/>).
    /// </summary>
    /// <param name="attacker">The attacking player.</param>
    /// <param name="defender">The attacked player.</param>
    public static void RegisterCombat(Player attacker, Player defender)
    {
        var now = DateTime.UtcNow;
        attacker.LastPvpCombatUtc = now;
        defender.LastPvpCombatUtc = now;
    }
}
