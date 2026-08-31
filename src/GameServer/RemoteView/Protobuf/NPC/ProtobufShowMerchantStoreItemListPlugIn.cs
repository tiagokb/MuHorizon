// <copyright file="ProtobufShowMerchantStoreItemListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufShowMerchantStoreItemListPlugIn : IShowMerchantStoreItemListPlugIn
{
    private readonly ProtobufRemotePlayer _player;
    private readonly ItemPriceCalculator _priceCalc = new();

    public ProtobufShowMerchantStoreItemListPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ShowMerchantStoreItemListAsync(ICollection<Item> storeItems, StoreKind storeKind)
    {
        var npc = _player.OpenedNpc;
        var msg = new S2CShopItems
        {
            NpcId   = npc is not null ? (uint)npc.Id : 0,
            NpcName = npc?.Definition.Designation ?? string.Empty,
        };

        foreach (var item in storeItems)
        {
            if (item.Definition is null) continue;
            var price = _priceCalc.CalculateFinalBuyingPrice(item);
            var opts  = item.ItemOptions;
            msg.Items.Add(new ShopItem
            {
                Slot         = item.ItemSlot,
                Number       = (uint)item.Definition.Number,
                Group        = item.Definition.Group,
                Price        = (ulong)price,
                Level        = item.Level,
                HasSkill     = item.HasSkill,
                HasLuck      = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck),
                HasOption    = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Option),
                HasExcellent = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent),
                HasAncient   = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.AncientOption),
                Width        = item.Definition.Width,
                Height       = item.Definition.Height,
            });
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { ShopItems = msg }).ConfigureAwait(false);

        // Send sell prices for the player's current inventory bag items.
        var priceMsg = new S2CSellPriceList();
        var invItems = _player.Inventory?.Items ?? _player.SelectedCharacter?.Inventory?.Items ?? [];
        foreach (var invItem in invItems.Where(i => i.Definition is not null))
        {
            var sellPrice = _priceCalc.CalculateSellingPrice(invItem, invItem.Durability());
            priceMsg.Entries.Add(new SellPriceEntry { Slot = invItem.ItemSlot, Price = (ulong)sellPrice });
        }

        await _player.SendEnvelopeAsync(new ServerEnvelope { SellPriceList = priceMsg }).ConfigureAwait(false);

        // Reset player state so subsequent interactions don't block on NpcDialogOpened.
        // OpenedNpc is kept so C2SBuyNpcItem can still resolve the store.
        await _player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
    }
}
