// <copyright file="GameTickLoop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

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

    private readonly GameMap _map;
    private readonly ILogger<GameTickLoop> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

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
                await ProcessInputsAsync(ct).ConfigureAwait(false);
                // Phase 3: TickEntities
                // Phase 3: BroadcastSnapshots
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
}
