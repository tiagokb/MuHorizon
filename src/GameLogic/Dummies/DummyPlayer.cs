// <copyright file="DummyPlayer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Dummies;

using System.Threading;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Inputs;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// What a test dummy does.
/// </summary>
public enum DummyBehavior
{
    /// <summary>
    /// Stands still and heals itself to full: something to hit (damage, PvP).
    /// </summary>
    Punchbag,

    /// <summary>
    /// Stuns the player who spawned it whenever it's in range: no walking, attacking or casting.
    /// </summary>
    Stun,

    /// <summary>
    /// Slows the player who spawned it (the Iced effect of the ice skills).
    /// </summary>
    Slow,

    /// <summary>
    /// Roots the player who spawned it (the Freeze effect of Ice Arrow): no walking, but attacking and casting.
    /// </summary>
    Root,

    /// <summary>
    /// Accepts trades, puts zen on the table and confirms when the partner confirms.
    /// </summary>
    Trade,
}

/// <summary>
/// A connection-less player of a test account which runs a scripted <see cref="DummyBehavior"/>
/// instead of the offline-player intelligence (see <see cref="DummyManager"/>). Its actions go
/// through its input queue, so they run in its map's tick like a real player's.
/// </summary>
public sealed class DummyPlayer : OfflinePlayer
{
    private static readonly TimeSpan PunchbagInterval = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan CrowdControlInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The range in tiles within which the crowd control dummies reach their target.
    /// </summary>
    private const int CrowdControlRangeTiles = 6;

    // Magic effect numbers (MagicEffectNumber of the initialization).
    private const short StunnedEffect = 0x3D;
    private const short IcedEffect = 0x38;
    private const short FreezeEffect = 0x39;

    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyPlayer"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="behavior">The behavior.</param>
    /// <param name="target">The player the dummy acts on (who spawned it).</param>
    public DummyPlayer(IGameContext gameContext, DummyBehavior behavior, Player target)
        : base(gameContext)
    {
        this.Behavior = behavior;
        this.Target = target;

        // Test opponents: they take part in PvP wherever the map allows it.
        this.PvpEnabled = true;
    }

    /// <summary>
    /// Gets the behavior.
    /// </summary>
    public DummyBehavior Behavior { get; }

    /// <summary>
    /// Gets the player the dummy acts on.
    /// </summary>
    public Player Target { get; }

    /// <inheritdoc />
    public override async ValueTask StopAsync()
    {
        await this._cts.CancelAsync().ConfigureAwait(false);
        await base.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new DummyViewPlugInContainer(this);

    /// <inheritdoc />
    protected override void StartIntelligence()
    {
        var interval = this.Behavior is DummyBehavior.Punchbag or DummyBehavior.Trade ? PunchbagInterval : CrowdControlInterval;
        _ = Task.Run(() => this.RunAsync(interval, this._cts.Token));
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await this._cts.CancelAsync().ConfigureAwait(false);
        this._cts.Dispose();
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    private async Task RunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                if (this.InputQueue.Count < 4)
                {
                    this.InputQueue.Enqueue(new ActionInput(_ => this.ActAsync()));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    private ValueTask ActAsync() => this.Behavior switch
    {
        DummyBehavior.Stun => this.ApplyCrowdControlAsync(StunnedEffect, TimeSpan.FromSeconds(2), "stunned"),
        DummyBehavior.Slow => this.ApplyCrowdControlAsync(IcedEffect, TimeSpan.FromSeconds(4), "slowed"),
        DummyBehavior.Root => this.ApplyCrowdControlAsync(FreezeEffect, TimeSpan.FromSeconds(3), "rooted"),
        _ => this.HealToFullAsync(),
    };

    private ValueTask HealToFullAsync()
    {
        if (this.IsAlive && this.Attributes is { } attributes)
        {
            attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
            attributes[Stats.CurrentShield] = attributes[Stats.MaximumShield];
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask ApplyCrowdControlAsync(short effectNumber, TimeSpan duration, string verb)
    {
        var target = this.Target;
        if (!this.IsAlive || !target.IsAlive || target.CurrentMap is null || target.CurrentMap != this.CurrentMap
            || !this.IsInRange(target.Position, CrowdControlRangeTiles)
            || !PvpRules.CanAttack(this, target))
        {
            return;
        }

        this.Rotation = this.GetDirectionTo(target);
        await this.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(this, 0, target, this.Rotation), true).ConfigureAwait(false);
        if (await this.ApplyMagicEffectAsync(target, effectNumber, duration).ConfigureAwait(false))
        {
            await target.ShowBlueMessageAsync($"{this.Name} {verb} you ({duration.TotalSeconds:0} s).").ConfigureAwait(false);
        }
    }
}
