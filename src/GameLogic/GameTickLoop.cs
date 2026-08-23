// <copyright file="GameTickLoop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Drives the per-map game loop at a fixed rate (default 30 Hz).
/// Each tick runs three sequential phases:
///   1. ProcessInputs  — dequeue and apply all pending IPlayerInput for every player.
///   2. TickEntities   — advance IGameTickable objects (walkers, AI, cooldowns — Phase 3+).
///   3. BroadcastSnapshots — send position deltas to observers (Phase 3+).
/// </summary>
public sealed class GameTickLoop
{
    private const int TargetTickMs = 33;
    private const int OvershotWarningMs = 20;

    // TODO: read from GameConfiguration
    private const float MoveSpeedUnitsPerSec = 80f;

    private readonly GameMap _map;
    private readonly ILogger<GameTickLoop> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    // Last tile position sent for each NPC/monster — used to detect movement between ticks.
    private readonly Dictionary<int, Point> _lastNpcPositions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTickLoop"/> class.
    /// </summary>
    public GameTickLoop(GameMap map, ILogger<GameTickLoop> logger)
    {
        _map = map;
        _logger = logger;
    }

    /// <summary>
    /// Starts the tick loop in a background task.
    /// </summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Signals the loop to stop and awaits its completion.
    /// </summary>
    public async ValueTask StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogDebug("GameTickLoop started for map {MapId}", _map.MapId);
        var sw = Stopwatch.StartNew();
        var lastTick = sw.Elapsed;

        while (!ct.IsCancellationRequested)
        {
            var tickStart = sw.Elapsed;
            var deltaTime = tickStart - lastTick;
            lastTick = tickStart;

            // Clamp delta to 3× target to avoid "teleports" after GC pauses.
            if (deltaTime > TimeSpan.FromMilliseconds(TargetTickMs * 3))
            {
                deltaTime = TimeSpan.FromMilliseconds(TargetTickMs * 3);
            }

            try
            {
                ResetMovementFlags();
                await ProcessInputsAsync(ct).ConfigureAwait(false);
                TickMovement(deltaTime);
                await TickNpcsAsync(deltaTime, ct).ConfigureAwait(false);
                await TickProjectilesAsync(deltaTime, ct).ConfigureAwait(false);
                await TickDropExpirationAsync(ct).ConfigureAwait(false);
                await UpdateTilePositionsAsync(ct).ConfigureAwait(false);
                await BroadcastSnapshotsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled exception in game tick for map {MapId}", _map.MapId);
            }

            var tickMs = (sw.Elapsed - tickStart).TotalMilliseconds;
            if (tickMs > OvershotWarningMs)
            {
                _logger.LogWarning("Tick overshoot on map {MapId}: {Elapsed:F1}ms (target {Target}ms)", _map.MapId, tickMs, TargetTickMs);
            }

            var remaining = TargetTickMs - (int)(sw.Elapsed - tickStart).TotalMilliseconds;
            if (remaining > 0)
            {
                try
                {
                    await Task.Delay(remaining, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogDebug("GameTickLoop stopped for map {MapId}", _map.MapId);
    }

    private async ValueTask TickNpcsAsync(TimeSpan deltaTime, CancellationToken ct)
    {
        foreach (var monster in _map.Monsters)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await monster.TickAsync(deltaTime).ConfigureAwait(false);
        }
    }

    private async ValueTask TickProjectilesAsync(TimeSpan deltaTime, CancellationToken ct)
    {
        foreach (var projectile in _map.Projectiles.ToList())
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await projectile.TickAsync(deltaTime).ConfigureAwait(false);
        }
    }

    private async ValueTask TickDropExpirationAsync(CancellationToken ct)
    {
        var expireBefore = DateTime.UtcNow - _map.ItemDropDuration;

        foreach (var drop in _map.DroppedItems.Where(d => d.DroppedAt < expireBefore).ToList())
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            await drop.ExpireAsync().ConfigureAwait(false);
        }

        foreach (var money in _map.DroppedMoneys.Where(m => m.DroppedAt < expireBefore).ToList())
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            await money.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ResetMovementFlags()
    {
        foreach (var player in _map.Players)
        {
            player.HasMovedThisTick = false;
        }
    }

    private async ValueTask ProcessInputsAsync(CancellationToken ct)
    {
        foreach (var player in _map.Players)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            while (player.InputQueue.TryDequeue(out var input))
            {
                await input.HandleAsync(player).ConfigureAwait(false);
            }
        }
    }

    private void TickMovement(TimeSpan deltaTime)
    {
        var dt = (float)deltaTime.TotalSeconds;
        foreach (var player in _map.Players)
        {
            if (player.ContinuousWalkTarget is not { } target)
            {
                continue;
            }

            var pos = player.ContinuousPosition;
            var dx = target.X - pos.X;
            var dy = target.Y - pos.Y;
            var distSq = dx * dx + dy * dy;
            var stepDist = MoveSpeedUnitsPerSec * dt;

            if (distSq <= stepDist * stepDist)
            {
                player.ContinuousPosition = target;
                player.ContinuousWalkTarget = null;
            }
            else
            {
                var dist = MathF.Sqrt(distSq);
                player.ContinuousPosition = new Vector2F(
                    pos.X + dx / dist * stepDist,
                    pos.Y + dy / dist * stepDist);
            }

            player.HasMovedThisTick = true;
        }
    }

    private async ValueTask UpdateTilePositionsAsync(CancellationToken ct)
    {
        foreach (var player in _map.Players)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!player.HasMovedThisTick)
            {
                continue;
            }

            var newTile = player.ContinuousPosition.ToTile();
            await player.UpdateMapTileAsync(newTile).ConfigureAwait(false);
        }
    }

    private async ValueTask BroadcastSnapshotsAsync(CancellationToken ct)
    {
        var positions = _map.Players
            .Where(p => p.HasMovedThisTick)
            .Select(p => ((uint)p.Id, p.ContinuousPosition.X, p.ContinuousPosition.Y))
            .ToList();

        // Include monsters whose tile position changed since the last broadcast.
        foreach (var monster in _map.Monsters)
        {
            var pos = monster.Position;
            if (!_lastNpcPositions.TryGetValue(monster.Id, out var last) || last != pos)
            {
                _lastNpcPositions[monster.Id] = pos;
                var wp = Vector2F.FromTile(pos);
                positions.Add(((uint)monster.Id, wp.X, wp.Y));
            }
        }

        if (positions.Count == 0)
        {
            return;
        }

        foreach (var player in _map.Players)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await player.InvokeViewPlugInAsync<ISnapshotPlugIn>(p => p.SendSnapshotAsync(positions)).ConfigureAwait(false);
        }
    }
}
