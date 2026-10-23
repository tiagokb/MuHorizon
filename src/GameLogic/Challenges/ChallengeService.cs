// <copyright file="ChallengeService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Challenges;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The phase of a <see cref="Challenge"/>.
/// </summary>
public enum ChallengePhase
{
    /// <summary>
    /// The teams are being brought to the arena.
    /// </summary>
    Starting,

    /// <summary>
    /// The countdown before a round.
    /// </summary>
    Countdown,

    /// <summary>
    /// The round is being fought.
    /// </summary>
    Fighting,

    /// <summary>
    /// A round ended; the next one starts soon.
    /// </summary>
    RoundEnd,

    /// <summary>
    /// The challenge is decided; everyone goes back soon.
    /// </summary>
    Finished,
}

/// <summary>
/// A challenge between two teams (parties, or single players) in a Duel Arena room, best of N rounds.
/// Dying only takes a player out of the round; no rewards, no penalties.
/// </summary>
public sealed class Challenge
{
    /// <summary>
    /// Gets the first team (the challengers).
    /// </summary>
    public List<Player> TeamA { get; } = new();

    /// <summary>
    /// Gets the second team.
    /// </summary>
    public List<Player> TeamB { get; } = new();

    /// <summary>
    /// Gets or sets the number of rounds (1, 3, 5).
    /// </summary>
    public int BestOf { get; set; }

    /// <summary>
    /// Gets the rounds a team needs to win.
    /// </summary>
    public int WinsNeeded => (this.BestOf / 2) + 1;

    /// <summary>
    /// Gets or sets the rounds won by team A.
    /// </summary>
    public int ScoreA { get; set; }

    /// <summary>
    /// Gets or sets the rounds won by team B.
    /// </summary>
    public int ScoreB { get; set; }

    /// <summary>
    /// Gets or sets the current round (1-based).
    /// </summary>
    public int Round { get; set; }

    /// <summary>
    /// Gets or sets the phase.
    /// </summary>
    public ChallengePhase Phase { get; set; }

    /// <summary>
    /// Gets or sets when the phase ends.
    /// </summary>
    public DateTime PhaseEndsAt { get; set; }

    /// <summary>
    /// Gets or sets the winner team of the challenge (0 = A, 1 = B), once decided.
    /// </summary>
    public int? Winner { get; set; }

    /// <summary>
    /// Gets the players who are out of the current round.
    /// </summary>
    public HashSet<Player> Out { get; } = new();

    /// <summary>
    /// Gets where each player goes back to after the challenge.
    /// </summary>
    public Dictionary<Player, (GameMapDefinition Map, byte X, byte Y)> ReturnPoints { get; } = new();

    /// <summary>
    /// Gets or sets the Duel Arena room (its gates: where the teams start, where the out wait).
    /// </summary>
    public DuelArea Area { get; set; } = null!;

    /// <summary>
    /// Gets or sets the map instance of the challenge: every challenge gets its own copy of the
    /// Duel Arena, so there's no limit of rooms and nobody else gets in.
    /// </summary>
    public GameMap Map { get; set; } = null!;

    /// <summary>
    /// Gets the lock for changes of the challenge.
    /// </summary>
    internal SemaphoreSlim Lock { get; } = new(1, 1);

    /// <summary>
    /// Gets all players of both teams.
    /// </summary>
    public IEnumerable<Player> Players => this.TeamA.Concat(this.TeamB);

    /// <summary>
    /// Gets the team (0 = A, 1 = B) of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The team index.</returns>
    public int TeamOf(Player player) => this.TeamA.Contains(player) ? 0 : 1;
}

/// <summary>
/// Challenges (see <see cref="Challenge"/>): requests, the arena, rounds and the result.
/// </summary>
public static class ChallengeService
{
    /// <summary>
    /// The map of the challenges.
    /// </summary>
    public const short DuelArenaMapNumber = 64;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartingTime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CountdownTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RoundEndTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FinishedTime = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<Player, Challenge> ByPlayer = new();
    private static readonly ConcurrentDictionary<Player, (Player Challenger, int BestOf, DateTime At)> Requests = new();

    /// <summary>
    /// Gets the challenge of the player, if any.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The challenge.</returns>
    public static Challenge? Of(Player player) => ByPlayer.TryGetValue(player, out var challenge) ? challenge : null;

    /// <summary>
    /// Gets the map instance the player enters for its challenge, if any.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The challenge's map instance.</returns>
    public static GameMap? InstanceMapOf(Player player) => Of(player)?.Map;

    /// <summary>
    /// Decides whether one player may attack another because of a challenge.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="defender">The defender.</param>
    /// <returns><c>true</c>/<c>false</c> if a challenge decides it; <c>null</c> if no challenge is involved.</returns>
    public static bool? CanAttack(Player attacker, Player defender)
    {
        var challenge = Of(attacker);
        var defenderChallenge = Of(defender);
        if (challenge is null && defenderChallenge is null)
        {
            return null;
        }

        return challenge == defenderChallenge
               && challenge!.Phase == ChallengePhase.Fighting
               && challenge.TeamOf(attacker) != challenge.TeamOf(defender)
               && !challenge.Out.Contains(attacker)
               && !challenge.Out.Contains(defender);
    }

    /// <summary>
    /// A player challenges another (their teams: the parties, or the players alone).
    /// </summary>
    /// <param name="challenger">The challenger.</param>
    /// <param name="target">The challenged player.</param>
    /// <param name="bestOf">The number of rounds: 1, 3 or 5.</param>
    public static async ValueTask RequestAsync(Player challenger, Player target, int bestOf)
    {
        bestOf = bestOf switch { >= 5 => 5, >= 3 => 3, _ => 1 };
        var (challengerLeader, challengerTeam) = TeamOf(challenger);
        var (targetLeader, targetTeam) = TeamOf(target);
        if (challengerLeader != challenger)
        {
            await challenger.ShowBlueMessageAsync("Only the party leader can challenge.").ConfigureAwait(false);
            return;
        }

        if (challengerTeam.Intersect(targetTeam).Any())
        {
            await challenger.ShowBlueMessageAsync("You can't challenge your own party.").ConfigureAwait(false);
            return;
        }

        if ((WhyNotAvailable(challengerTeam) ?? WhyNotAvailable(targetTeam)) is { } reason)
        {
            await challenger.ShowBlueMessageAsync(reason).ConfigureAwait(false);
            return;
        }

        Requests[targetLeader] = (challenger, bestOf, DateTime.UtcNow);
        await targetLeader.InvokeViewPlugInAsync<IChallengeViewPlugIn>(p => p.ShowChallengeRequestAsync(challenger, challengerTeam.Count, targetTeam.Count, bestOf)).ConfigureAwait(false);
        await challenger.ShowBlueMessageAsync($"Challenge sent to {targetLeader.Name}'s team ({challengerTeam.Count} vs {targetTeam.Count}, best of {bestOf}).").ConfigureAwait(false);
    }

    /// <summary>
    /// The challenged leader answers the last challenge.
    /// </summary>
    /// <param name="leader">The leader of the challenged team.</param>
    /// <param name="accepted">Whether it was accepted.</param>
    public static async ValueTask AnswerAsync(Player leader, bool accepted)
    {
        if (!Requests.TryRemove(leader, out var request) || DateTime.UtcNow - request.At > RequestTimeout)
        {
            await leader.ShowBlueMessageAsync("The challenge expired.").ConfigureAwait(false);
            return;
        }

        var challenger = request.Challenger;
        if (!accepted)
        {
            await challenger.ShowBlueMessageAsync($"{leader.Name} declined the challenge.").ConfigureAwait(false);
            return;
        }

        var (_, teamA) = TeamOf(challenger);
        var (_, teamB) = TeamOf(leader);
        if ((WhyNotAvailable(teamA) ?? WhyNotAvailable(teamB)) is { } reason)
        {
            await leader.ShowBlueMessageAsync(reason).ConfigureAwait(false);
            await challenger.ShowBlueMessageAsync(reason).ConfigureAwait(false);
            return;
        }

        var configuration = leader.GameContext.Configuration;
        if (configuration.DuelConfiguration?.DuelAreas.FirstOrDefault(a => a.FirstPlayerGate is not null && a.SecondPlayerGate is not null) is not { } area
            || configuration.Maps.FirstOrDefault(m => m.Number == DuelArenaMapNumber) is not { } arenaDefinition
            || leader.GameContext is not GameContext gameContext)
        {
            await challenger.ShowBlueMessageAsync("The Duel Arena isn't configured.").ConfigureAwait(false);
            await leader.ShowBlueMessageAsync("The Duel Arena isn't configured.").ConfigureAwait(false);
            return;
        }

        var challenge = new Challenge
        {
            BestOf = request.BestOf,
            Area = area,
            Map = await gameContext.CreateInstanceMapAsync(arenaDefinition).ConfigureAwait(false),
            Phase = ChallengePhase.Starting,
            PhaseEndsAt = DateTime.UtcNow + StartingTime,
        };
        challenge.TeamA.AddRange(teamA);
        challenge.TeamB.AddRange(teamB);
        foreach (var player in challenge.Players)
        {
            challenge.ReturnPoints[player] = (player.CurrentMap!.Definition, player.Position.X, player.Position.Y);
            ByPlayer[player] = challenge;
        }

        foreach (var player in challenge.Players)
        {
            await player.WarpToAsync(GateOf(challenge, player)!).ConfigureAwait(false);
        }

        await ShowStateAsync(challenge).ConfigureAwait(false);
    }

    /// <summary>
    /// A player of a challenge took a lethal hit: instead of dying, they're out of the round.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if the challenge took care of it (the player doesn't die).</returns>
    public static async ValueTask<bool> TryKnockOutAsync(Player player)
    {
        if (Of(player) is not { } challenge)
        {
            return false;
        }

        await challenge.Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await RestoreAsync(player).ConfigureAwait(false);
            if (challenge.Phase != ChallengePhase.Fighting || !challenge.Out.Add(player))
            {
                return true;
            }

            await player.MoveAsync(SpectatorPoint(challenge)).ConfigureAwait(false);
            await ShowMessageToAllAsync(challenge, $"{player.Name} is out!").ConfigureAwait(false);
            await CheckRoundEndAsync(challenge).ConfigureAwait(false);
        }
        finally
        {
            challenge.Lock.Release();
        }

        return true;
    }

    /// <summary>
    /// Advances the player's challenge (called by the tick loop).
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask UpdateAsync(Player player)
    {
        if (Of(player) is not { } challenge || DateTime.UtcNow < challenge.PhaseEndsAt && challenge.Phase != ChallengePhase.Fighting)
        {
            return;
        }

        if (!await challenge.Lock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await AdvanceAsync(challenge).ConfigureAwait(false);
        }
        finally
        {
            challenge.Lock.Release();
        }
    }

    /// <summary>
    /// The player leaves the game: they leave the challenge too (a team without players loses).
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask LeaveAsync(Player player)
    {
        Requests.TryRemove(player, out _);
        if (!ByPlayer.TryRemove(player, out var challenge))
        {
            return;
        }

        await challenge.Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            challenge.TeamA.Remove(player);
            challenge.TeamB.Remove(player);
            challenge.Out.Remove(player);
            if (challenge.ReturnPoints.Remove(player, out var back) && player.SelectedCharacter is { } character)
            {
                // Logged out in the arena: next login starts where the challenge began.
                character.CurrentMap = back.Map;
                character.PositionX = back.X;
                character.PositionY = back.Y;
            }

            if (challenge.TeamA.Count == 0 && challenge.TeamB.Count == 0)
            {
                await challenge.Map.StopTickLoopAsync().ConfigureAwait(false);
            }
            else if (challenge.Phase != ChallengePhase.Finished && (challenge.TeamA.Count == 0 || challenge.TeamB.Count == 0))
            {
                await FinishAsync(challenge, challenge.TeamA.Count == 0 ? 1 : 0, "The other team left.").ConfigureAwait(false);
            }
            else
            {
                await CheckRoundEndAsync(challenge).ConfigureAwait(false);
            }
        }
        finally
        {
            challenge.Lock.Release();
        }
    }

    private static async ValueTask AdvanceAsync(Challenge challenge)
    {
        var now = DateTime.UtcNow;
        switch (challenge.Phase)
        {
            case ChallengePhase.Fighting:
                // Someone who left the arena (warp, portal) is out.
                foreach (var player in challenge.Players.Where(p => p.CurrentMap != challenge.Map && !challenge.Out.Contains(p)).ToList())
                {
                    challenge.Out.Add(player);
                }

                await CheckRoundEndAsync(challenge).ConfigureAwait(false);
                break;
            case ChallengePhase.Starting or ChallengePhase.RoundEnd when now >= challenge.PhaseEndsAt:
                await StartRoundAsync(challenge).ConfigureAwait(false);
                break;
            case ChallengePhase.Countdown when now >= challenge.PhaseEndsAt:
                challenge.Phase = ChallengePhase.Fighting;
                await ShowMessageToAllAsync(challenge, "Fight!").ConfigureAwait(false);
                await ShowStateAsync(challenge).ConfigureAwait(false);
                break;
            case ChallengePhase.Finished when now >= challenge.PhaseEndsAt:
                await SendBackAsync(challenge).ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask StartRoundAsync(Challenge challenge)
    {
        challenge.Round++;
        challenge.Out.Clear();
        challenge.Phase = ChallengePhase.Countdown;
        challenge.PhaseEndsAt = DateTime.UtcNow + CountdownTime;
        foreach (var player in challenge.Players)
        {
            await RestoreAsync(player).ConfigureAwait(false);
            if (player.CurrentMap == challenge.Map && GateOf(challenge, player) is { } gate)
            {
                await player.MoveAsync(gate.GetRandomPoint()).ConfigureAwait(false);
            }
        }

        await ShowMessageToAllAsync(challenge, $"Round {challenge.Round} — {challenge.ScoreA} : {challenge.ScoreB}").ConfigureAwait(false);
        await ShowStateAsync(challenge).ConfigureAwait(false);
    }

    private static async ValueTask CheckRoundEndAsync(Challenge challenge)
    {
        if (challenge.Phase != ChallengePhase.Fighting)
        {
            return;
        }

        var aleft = challenge.TeamA.Any(p => !challenge.Out.Contains(p));
        var bleft = challenge.TeamB.Any(p => !challenge.Out.Contains(p));
        if (aleft && bleft)
        {
            await ShowStateAsync(challenge).ConfigureAwait(false);
            return;
        }

        // The team with someone left wins the round (the last one standing).
        var roundWinner = aleft ? 0 : 1;
        if (roundWinner == 0)
        {
            challenge.ScoreA++;
        }
        else
        {
            challenge.ScoreB++;
        }

        if (challenge.ScoreA >= challenge.WinsNeeded || challenge.ScoreB >= challenge.WinsNeeded)
        {
            await FinishAsync(challenge, roundWinner, null).ConfigureAwait(false);
            return;
        }

        challenge.Phase = ChallengePhase.RoundEnd;
        challenge.PhaseEndsAt = DateTime.UtcNow + RoundEndTime;
        await ShowMessageToAllAsync(challenge, $"Round {challenge.Round} to team {(roundWinner == 0 ? challenge.TeamA : challenge.TeamB).FirstOrDefault()?.Name} — {challenge.ScoreA} : {challenge.ScoreB}").ConfigureAwait(false);
        await ShowStateAsync(challenge).ConfigureAwait(false);
    }

    private static async ValueTask FinishAsync(Challenge challenge, int winner, string? reason)
    {
        challenge.Winner = winner;
        challenge.Phase = ChallengePhase.Finished;
        challenge.PhaseEndsAt = DateTime.UtcNow + FinishedTime;
        var winners = winner == 0 ? challenge.TeamA : challenge.TeamB;
        await ShowMessageToAllAsync(challenge, $"{reason} Team {winners.FirstOrDefault()?.Name} wins {challenge.ScoreA} : {challenge.ScoreB}!".TrimStart()).ConfigureAwait(false);
        await ShowStateAsync(challenge).ConfigureAwait(false);
    }

    private static async ValueTask SendBackAsync(Challenge challenge)
    {
        foreach (var player in challenge.Players.ToList())
        {
            ByPlayer.TryRemove(player, out _);
            await RestoreAsync(player).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IChallengeViewPlugIn>(p => p.ShowChallengeStateAsync(null, player)).ConfigureAwait(false);
            if (challenge.ReturnPoints.TryGetValue(player, out var back))
            {
                await player.WarpToAsync(new ExitGate { Map = back.Map, X1 = back.X, X2 = back.X, Y1 = back.Y, Y2 = back.Y }).ConfigureAwait(false);
            }
        }

        challenge.TeamA.Clear();
        challenge.TeamB.Clear();

        // Everyone left the instance: stop it.
        await challenge.Map.StopTickLoopAsync().ConfigureAwait(false);
    }

    private static async ValueTask RestoreAsync(Player player)
    {
        if (player.Attributes is not { } attributes)
        {
            return;
        }

        attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
        attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
        attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
        await player.InvokeViewPlugInAsync<IUpdateStatsPlugIn>(p => p.UpdateStatsAsync(Stats.CurrentHealth, attributes[Stats.CurrentHealth])).ConfigureAwait(false);
    }

    private static (Player Leader, List<Player> Team) TeamOf(Player player) =>
        player.Party is { } party
            ? ((party.PartyMaster as Player) ?? player, party.PartyList.OfType<Player>().ToList())
            : (player, [player]);

    private static string? WhyNotAvailable(IEnumerable<Player> team)
    {
        foreach (var player in team)
        {
            if (Of(player) is not null)
            {
                return $"{player.Name} is already in a challenge.";
            }

            if (player.CurrentMiniGame is not null || player.DuelRoom is not null || player.CurrentMap is null || !player.IsAlive)
            {
                return $"{player.Name} can't fight a challenge now (event, instance or not in the world).";
            }
        }

        return null;
    }

    private static ExitGate? GateOf(Challenge challenge, Player player) =>
        challenge.TeamOf(player) == 0 ? challenge.Area.FirstPlayerGate : challenge.Area.SecondPlayerGate;

    private static Point SpectatorPoint(Challenge challenge) =>
        challenge.Area.SpectatorsGate?.GetRandomPoint()
        ?? challenge.Area.FirstPlayerGate!.GetRandomPoint();

    private static async ValueTask ShowMessageToAllAsync(Challenge challenge, string message)
    {
        foreach (var player in challenge.Players)
        {
            await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
        }
    }

    private static async ValueTask ShowStateAsync(Challenge challenge)
    {
        foreach (var player in challenge.Players)
        {
            await player.InvokeViewPlugInAsync<IChallengeViewPlugIn>(p => p.ShowChallengeStateAsync(challenge, player)).ConfigureAwait(false);
        }
    }
}
