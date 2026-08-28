// <copyright file="ProtobufUpdateInventoryListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufUpdateInventoryListPlugIn : IUpdateInventoryListPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufUpdateInventoryListPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask UpdateInventoryListAsync()
    {
        var items = (_player.Inventory?.Items
                  ?? _player.SelectedCharacter?.Inventory?.Items
                  ?? Enumerable.Empty<Item>())
            .Where(i => i.Definition is not null)
            .Select(BuildItemData);

        var msg = new S2CInventory();
        msg.Items.AddRange(items);
        return _player.SendEnvelopeAsync(new ServerEnvelope { Inventory = msg });
    }

    internal static ItemData BuildItemData(Item item)
    {
        var opts = item.ItemOptions;
        return new ItemData
        {
            Slot         = item.ItemSlot,
            Number       = (uint)item.Definition!.Number,
            Group        = item.Definition.Group,
            Level        = item.Level,
            HasSkill     = item.HasSkill,
            HasLuck      = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck),
            HasOption    = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Option),
            HasExcellent = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent),
            HasAncient   = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.AncientOption),
        };
    }
}
