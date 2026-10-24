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
/// <item>Mini games which allow killing keep their own rules (challenges are checked before, see <see cref="CanAffect"/>).</item>
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
        if (attacker == defender)
        {
            return false;
        }

        // A challenge decides between its players, and keeps them out of other fights.
        if (Challenges.ChallengeService.CanAttack(attacker, defender) is { } challengeDecision)
        {
            return challengeDecision;
        }

        if (attacker.IsAtSafezone() || defender.IsAtSafezone())
        {
            return false;
        }

        if (attacker.Party is { } party && party == defender.Party)
        {
            return false;
        }

        if (defender.CurrentMiniGame?.AllowPlayerKilling is true)
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
    /// <summary>
    /// Decides whether a skill effect may be applied to the target: harmful effects (slow, stun,
    /// debuffs) only on players who may be attacked; helpful ones (buffs) not across a challenge.
    /// Effects on monsters, or on oneself, are always allowed here.
    /// </summary>
    /// <param name="source">The one applying the effect.</param>
    /// <param name="target">The target.</param>
    /// <param name="harmful">Whether the effect is harmful.</param>
    /// <returns><c>true</c> if the effect may be applied.</returns>
    public static bool CanAffect(IAttacker source, IAttackable target, bool harmful)
    {
        if (target is not Player defender || GetAttackingPlayer(source) is not { } player || player == defender)
        {
            return true;
        }

        if (harmful)
        {
            return CanAttack(player, defender);
        }

        var challenge = Challenges.ChallengeService.Of(player);
        return challenge == Challenges.ChallengeService.Of(defender)
               && (challenge is null || challenge.TeamOf(player) == challenge.TeamOf(defender));
    }

    public static void RegisterCombat(Player attacker, Player defender)
    {
        var now = DateTime.UtcNow;
        attacker.LastPvpCombatUtc = now;
        defender.LastPvpCombatUtc = now;
    }
}
