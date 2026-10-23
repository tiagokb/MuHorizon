// <copyright file="DummyViewPlugInContainer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Dummies;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Inputs;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlayerActions.Trade;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The views of a test dummy: those of an offline player, plus trading for the trade dummy.
/// </summary>
internal sealed class DummyViewPlugInContainer : ICustomPlugInContainer<IViewPlugIn>
{
    private readonly OfflineViewPlugInContainer _offline;
    private readonly DummyTradeView _trade;
    private readonly DummyFriendView _friend;
    private readonly DummyChallengeView _challenge;

    public DummyViewPlugInContainer(DummyPlayer dummy)
    {
        _offline = new OfflineViewPlugInContainer(dummy);
        _trade = new DummyTradeView(dummy);
        _friend = new DummyFriendView(dummy);
        _challenge = new DummyChallengeView(dummy);
    }

    public T? GetPlugIn<T>()
        where T : class, IViewPlugIn
        => _trade as T ?? _friend as T ?? _challenge as T ?? _offline.GetPlugIn<T>();
}

/// <summary>
/// Dummies accept every challenge (something to try the challenge flow on).
/// </summary>
internal sealed class DummyChallengeView : Challenges.IChallengeViewPlugIn
{
    private readonly DummyPlayer _dummy;

    public DummyChallengeView(DummyPlayer dummy) => _dummy = dummy;

    public ValueTask ShowChallengeRequestAsync(Player challenger, int challengerTeamSize, int ownTeamSize, int bestOf)
    {
        _dummy.InputQueue.Enqueue(new ActionInput(_ => Challenges.ChallengeService.AnswerAsync(_dummy, true)));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowChallengeStateAsync(Challenges.Challenge? challenge, Player viewer) => ValueTask.CompletedTask;
}

/// <summary>
/// The friend dummy accepts every friend request.
/// </summary>
internal sealed class DummyFriendView : Views.Messenger.IShowFriendRequestPlugIn
{
    private static readonly PlayerActions.Messenger.AddResponseAction ResponseAction = new();

    private readonly DummyPlayer _dummy;

    public DummyFriendView(DummyPlayer dummy) => _dummy = dummy;

    public ValueTask ShowFriendRequestAsync(string requester)
    {
        if (_dummy.Behavior == DummyBehavior.Friend)
        {
            _dummy.InputQueue.Enqueue(new ActionInput(_ => ResponseAction.ProceedResponseAsync(_dummy, requester, true)));
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Lets the trade dummy (/dummy trade) answer trades: it accepts a request, puts
/// <see cref="GiftZen"/> on the table, and confirms whenever the partner confirms. Other dummies
/// refuse trades. The answers go through the dummy's input queue, so they run in its map's tick.
/// </summary>
internal sealed class DummyTradeView :
    IShowTradeRequestPlugIn,
    IShowTradeRequestAnswerPlugIn,
    IChangeTradeButtonStatePlugIn,
    ISetTradeMoneyPlugIn,
    IRequestedTradeMoneyHasBeenSetPlugIn,
    ITradeFinishedPlugIn,
    ITradeItemAppearPlugIn,
    ITradeItemDisappearPlugIn
{
    /// <summary>
    /// The zen the trade dummy gives in every trade.
    /// </summary>
    private const int GiftZen = 1_000_000;

    private static readonly TradeAcceptAction AcceptAction = new();
    private static readonly TradeMoneyAction MoneyAction = new();
    private static readonly TradeButtonAction ButtonAction = new();

    private readonly DummyPlayer _dummy;

    public DummyTradeView(DummyPlayer dummy) => _dummy = dummy;

    public ValueTask ShowTradeRequestAsync(ITrader requester)
    {
        var accept = _dummy.Behavior == DummyBehavior.Trade;
        _dummy.InputQueue.Enqueue(new ActionInput(async _ =>
        {
            await AcceptAction.HandleTradeAcceptAsync(_dummy, accept).ConfigureAwait(false);
            if (!accept)
            {
                return;
            }

            if (_dummy.Money < GiftZen)
            {
                _dummy.TryAddMoney(GiftZen - _dummy.Money);
            }

            await MoneyAction.TradeMoneyAsync(_dummy, GiftZen).ConfigureAwait(false);
        }));
        return ValueTask.CompletedTask;
    }

    // The partner confirmed: the dummy confirms too, which completes the trade.
    public ValueTask ChangeTradeButtonStateAsync(TradeButtonState state)
    {
        if (state == TradeButtonState.Checked && _dummy.Behavior == DummyBehavior.Trade)
        {
            _dummy.InputQueue.Enqueue(new ActionInput(_ => ButtonAction.TradeButtonChangedAsync(_dummy, TradeButtonState.Checked)));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ShowTradeRequestAnswerAsync(bool tradeAccepted) => ValueTask.CompletedTask;

    public ValueTask SetTradeMoneyAsync(uint moneyAmount) => ValueTask.CompletedTask;

    public ValueTask RequestedTradeMoneyHasBeenSetAsync() => ValueTask.CompletedTask;

    public ValueTask TradeFinishedAsync(TradeResult tradeResult)
    {
        _dummy.Logger.LogInformation("Dummy {Dummy} finished a trade: {Result}.", _dummy.Name, tradeResult);
        return ValueTask.CompletedTask;
    }

    public ValueTask TradeItemAppearAsync(byte toSlot, Item item) => ValueTask.CompletedTask;

    public ValueTask TradeItemDisappearAsync(byte fromSlot, Item item) => ValueTask.CompletedTask;
}
