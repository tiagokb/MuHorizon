// <copyright file="DummyManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Dummies;

using System.Collections.Concurrent;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Spawns and removes test dummies (/dummy). A dummy drives the first character of a test
/// account (test1..test9) which isn't online, and appears next to the player who spawned it.
/// </summary>
public static class DummyManager
{
    private static readonly string[] Accounts = ["test1", "test2", "test3", "test4", "test5", "test6", "test7", "test8", "test9"];

    private static readonly ConcurrentDictionary<DummyPlayer, byte> Active = new();

    /// <summary>
    /// Spawns a dummy next to the player.
    /// </summary>
    /// <param name="spawner">The player who spawns it (and whom it acts on).</param>
    /// <param name="behavior">The behavior.</param>
    /// <returns>The dummy; <c>null</c>, if no test account is free.</returns>
    public static async ValueTask<DummyPlayer?> SpawnAsync(Player spawner, DummyBehavior behavior)
    {
        if (spawner.CurrentMap is not { } map)
        {
            return null;
        }

        var players = await spawner.GameContext.GetPlayersAsync().ConfigureAwait(false);
        var busy = players.Select(p => p.Account?.LoginName).OfType<string>()
            .Concat(Active.Keys.Select(d => d.AccountLoginName).OfType<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var loginName in Accounts.Where(a => !busy.Contains(a)))
        {
            var dummy = new DummyPlayer(spawner.GameContext, behavior, spawner);
            var account = await dummy.PersistenceContext.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            if (account?.Characters.OrderBy(c => c.CharacterSlot).FirstOrDefault() is not { } character
                || !await dummy.InitializeAsync(loginName, character.Name).ConfigureAwait(false))
            {
                await dummy.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            Active[dummy] = 0;
            var spot = FindSpotNear(map, spawner.Position);
            await dummy.WarpToAsync(new ExitGate { Map = map.Definition, X1 = spot.X, X2 = spot.X, Y1 = spot.Y, Y2 = spot.Y }).ConfigureAwait(false);
            dummy.Logger.LogInformation("Dummy {Behavior} spawned as {Character} next to {Spawner}.", behavior, dummy.Name, spawner.Name);
            return dummy;
        }

        return null;
    }

    /// <summary>
    /// Removes the dummies of the player's game server.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The number of removed dummies.</returns>
    public static async ValueTask<int> ClearAsync(Player player)
    {
        var removed = 0;
        foreach (var dummy in Active.Keys.Where(d => d.GameContext == player.GameContext).ToList())
        {
            if (!Active.TryRemove(dummy, out _))
            {
                continue;
            }

            try
            {
                await dummy.StopAsync().ConfigureAwait(false);
                await dummy.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Failed to remove dummy {Dummy}.", dummy.Name);
            }

            removed++;
        }

        return removed;
    }

    // Two tiles away, on a walkable tile.
    private static Point FindSpotNear(GameMap map, Point center)
    {
        (int X, int Y)[] offsets = [(2, 0), (-2, 0), (0, 2), (0, -2), (2, 2), (-2, -2), (2, -2), (-2, 2), (1, 0), (0, 1)];
        foreach (var (dx, dy) in offsets)
        {
            var x = center.X + dx;
            var y = center.Y + dy;
            if (x is >= 0 and <= byte.MaxValue && y is >= 0 and <= byte.MaxValue && map.Terrain.WalkMap[x, y])
            {
                return new Point((byte)x, (byte)y);
            }
        }

        return center;
    }
}
