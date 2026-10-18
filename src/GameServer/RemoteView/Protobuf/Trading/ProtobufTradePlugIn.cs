// <copyright file="ProtobufTradePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Trading;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Trade views for the Protobuf client. Instead of the legacy deltas (an item appeared, the
/// partner's money, a button) it sends the whole table (S2CTradeState) after every change:
/// both sides' items and zen and both buttons.
/// </summary>
internal sealed class ProtobufTradePlugIn :
    IShowTradeRequestPlugIn,
    IShowTradeRequestAnswerPlugIn,
    IChangeTradeButtonStatePlugIn,
    ISetTradeMoneyPlugIn,
    IRequestedTradeMoneyHasBeenSetPlugIn,
    ITradeFinishedPlugIn,
    ITradeItemAppearPlugIn,
    ITradeItemDisappearPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    // Only the partner's button is told as a change; the own one follows the player state.
    private TradeButton _partnerButton;

    public ProtobufTradePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowTradeRequestAsync(ITrader requester) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            TradeRequest = new S2CTradeRequest
            {
                RequesterId = requester is Player requesterPlayer ? requesterPlayer.Id : 0u,
                RequesterName = requester.Name,
            },
        });

    public async ValueTask ShowTradeRequestAnswerAsync(bool tradeAccepted)
    {
        _partnerButton = TradeButton.Unchecked;
        if (!tradeAccepted)
        {
            await _player.ShowBlueMessageAsync("The trade was refused.").ConfigureAwait(false);
            return;
        }

        await this.SendStateAsync().ConfigureAwait(false);
    }

    public ValueTask ChangeTradeButtonStateAsync(TradeButtonState state)
    {
        _partnerButton = (TradeButton)state;
        return this.SendStateAsync();
    }

    public ValueTask SetTradeMoneyAsync(uint moneyAmount) => this.SendStateAsync();

    public ValueTask RequestedTradeMoneyHasBeenSetAsync() => this.SendStateAsync();

    public ValueTask TradeItemAppearAsync(byte toSlot, Item item) => this.SendStateAsync();

    public ValueTask TradeItemDisappearAsync(byte fromSlot, Item item) => this.SendStateAsync();

    public async ValueTask TradeFinishedAsync(TradeResult tradeResult)
    {
        _partnerButton = TradeButton.Unchecked;
        await _player.SendEnvelopeAsync(new ServerEnvelope { TradeFinished = new S2CTradeFinished { Result = tradeResult.ToString() } }).ConfigureAwait(false);
        await _player.SendEnvelopeAsync(new ServerEnvelope { TradeState = new S2CTradeState { Open = false } }).ConfigureAwait(false);

        // The traded items and zen are in the inventories now.
        await new ProtobufUpdateInventoryListPlugIn(_player).UpdateInventoryListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the whole table, if a trade is open.
    /// </summary>
    internal ValueTask SendStateAsync()
    {
        var current = _player.PlayerState.CurrentState;
        if (_player.TradingPartner is not { } partner
            || (current != PlayerState.TradeOpened && current != PlayerState.TradeButtonPressed))
        {
            return ValueTask.CompletedTask;
        }

        var state = new S2CTradeState
        {
            Open = true,
            PartnerName = partner.Name,
            PartnerLevel = (uint)Math.Max(0, partner.Level),
            OwnMoney = (uint)Math.Max(0, _player.TradingMoney),
            PartnerMoney = (uint)Math.Max(0, partner.TradingMoney),
            OwnChecked = _player.PlayerState.CurrentState == PlayerState.TradeButtonPressed,
            PartnerButton = _partnerButton,
        };
        AddItems(state.OwnItems, _player.TemporaryStorage);
        AddItems(state.PartnerItems, partner.TemporaryStorage);
        return _player.SendEnvelopeAsync(new ServerEnvelope { TradeState = state });
    }

    private static void AddItems(Google.Protobuf.Collections.RepeatedField<ItemData> target, GameLogic.IStorage? storage)
    {
        if (storage is null)
        {
            return;
        }

        foreach (var item in storage.Items.Where(i => i.Definition is not null))
        {
            target.Add(ProtobufUpdateInventoryListPlugIn.BuildItemData(item));
        }
    }
}
