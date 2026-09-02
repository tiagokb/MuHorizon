// <copyright file="ProtobufUpdateInventoryListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
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
        var optLink = opts.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option);

        var data = new ItemData
        {
            Slot         = item.ItemSlot,
            Number       = (uint)item.Definition!.Number,
            Group        = item.Definition.Group,
            Level        = item.Level,
            HasSkill     = item.HasSkill,
            HasLuck      = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck),
            HasOption    = optLink is not null,
            HasExcellent = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent),
            HasAncient   = opts.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.AncientOption),
            Width        = item.Definition.Width,
            Height       = item.Definition.Height,
            Durability   = item.Durability(),
            OptionLevel  = optLink is not null ? (uint)optLink.Level : 0,
        };

        foreach (var pu in item.Definition.BasePowerUpAttributes)
        {
            if (pu.TargetAttribute is not { } attr) continue;
            var val = (uint)MathF.Round(pu.BaseValue + GetLevelBonus(pu, item.Level));
            if (attr.Id == Stats.MinimumPhysBaseDmgByWeapon.Id || attr.Id == Stats.MinimumWizBaseDmg.Id)
                data.MinDmg = val;
            else if (attr.Id == Stats.MaximumPhysBaseDmgByWeapon.Id || attr.Id == Stats.MaximumWizBaseDmg.Id)
                data.MaxDmg = val;
            else if (attr.Id == Stats.DefenseBase.Id)
                data.Defense = val;
            else if (attr.Id == Stats.DefenseRatePvm.Id)
                data.DefenseRate = val;
            else if (attr.Id == Stats.StaffRise.Id || attr.Id == Stats.BookRise.Id || attr.Id == Stats.ScepterRise.Id)
                data.WizardryRise = val;
        }

        return data;
    }

    private static float GetLevelBonus(ItemBasePowerUpDefinition pu, byte level)
    {
        if (pu.BonusPerLevelTable is null) return 0f;
        return pu.BonusPerLevelTable.BonusPerLevel
            .FirstOrDefault(b => b.Level == level)?.AdditionalValue ?? 0f;
    }
}