// <copyright file="MakeMovementSpeedAdditivePlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Switches movement speed from the legacy "fastest item wins" model to an additive one:
/// a global base speed plus a bonus per item (boots, wings by tier, mounts), so movement
/// speed can be built up. Slows (<see cref="Stats.MovementSpeedFactor"/>) still multiply the total.
/// </summary>
public abstract class MakeMovementSpeedAdditivePlugInBase : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Make Movement Speed Additive";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Movement speed becomes a base value plus additive item bonuses (boots +1, wings +2/+3/+4 by tier, mounts +2).";

    private const byte WingGroup = (byte)ItemGroups.Orbs; // group 12 holds wings, capes and orbs
    private const byte PetItemGroup = (byte)ItemGroups.Misc1;
    private const byte UniriaNumber = 2;
    private const byte DinorantNumber = 3;
    private const byte DarkHorseNumber = 4;
    private const byte FenrirNumber = 37;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        this.AddBaseValue(context, gameConfiguration, Stats.MovementSpeed);
        this.AddBaseValue(context, gameConfiguration, Stats.MovementSpeedUnderwater);

        foreach (var item in gameConfiguration.Items)
        {
            if (GetLandBonus(item) is { } landBonus)
            {
                this.SetBonus(context, gameConfiguration, item, Stats.MovementSpeed, landBonus);
            }

            if (GetUnderwaterBonus(item) is { } underwaterBonus)
            {
                this.SetBonus(context, gameConfiguration, item, Stats.MovementSpeedUnderwater, underwaterBonus);
            }
        }

        foreach (var bonus in gameConfiguration.ItemOptionCombinationBonuses.Select(b => b.Bonus))
        {
            if (bonus?.Boost is { } boost
                && (bonus.TargetAttribute?.Id == Stats.MovementSpeed.Id || bonus.TargetAttribute?.Id == Stats.MovementSpeedUnderwater.Id))
            {
                boost.ConstantValue.Value = MovementSpeedConstants.UpgradedFenrirMovementSpeedBonus;
                boost.ConstantValue.AggregateType = AggregateType.AddRaw;
            }
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsWingSlotItem(ItemDefinition item) =>
        item.ItemSlot?.ItemSlots.Contains(InventoryConstants.WingsSlot) ?? false;

    private static bool IsMount(ItemDefinition item) =>
        item.Group == PetItemGroup && item.Number is UniriaNumber or DinorantNumber or DarkHorseNumber or FenrirNumber;

    private static float? GetLandBonus(ItemDefinition item) => item switch
    {
        _ when item.Group == (byte)ItemGroups.Boots => MovementSpeedConstants.RunningGearMovementSpeedBonus,
        _ => GetSharedBonus(item),
    };

    private static float? GetUnderwaterBonus(ItemDefinition item) => item switch
    {
        _ when item.Group == (byte)ItemGroups.Gloves => MovementSpeedConstants.RunningGearMovementSpeedBonus,
        _ => GetSharedBonus(item),
    };

    // Wings and mounts speed up both on land and underwater.
    private static float? GetSharedBonus(ItemDefinition item)
    {
        if (IsMount(item))
        {
            return MovementSpeedConstants.MountMovementSpeedBonus;
        }

        if (IsWingSlotItem(item))
        {
            return item.Group == WingGroup
                ? MovementSpeedConstants.GetWingMovementSpeedBonus(item.Number)
                : MovementSpeedConstants.FirstTierWingMovementSpeedBonus;
        }

        return null;
    }

    private void AddBaseValue(IContext context, GameConfiguration gameConfiguration, AttributeDefinition attribute)
    {
        if (gameConfiguration.GlobalBaseAttributeValues.Any(a => a.Definition?.Id == attribute.Id))
        {
            return;
        }

        gameConfiguration.GlobalBaseAttributeValues.Add(
            context.CreateNew<ConstValueAttribute>(MovementSpeedConstants.BaseMovementSpeed, attribute.GetPersistent(gameConfiguration)));
    }

    /// <summary>Replaces the item's (legacy, absolute) speed power-up with an additive bonus.</summary>
    private void SetBonus(IContext context, GameConfiguration gameConfiguration, ItemDefinition item, AttributeDefinition attribute, float bonus)
    {
        var powerUp = item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute?.Id == attribute.Id);
        if (powerUp is null)
        {
            powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
            powerUp.TargetAttribute = attribute.GetPersistent(gameConfiguration);
            item.BasePowerUpAttributes.Add(powerUp);
        }

        powerUp.BaseValue = bonus;
        powerUp.AggregateType = AggregateType.AddRaw;
        powerUp.BonusPerLevelTable = null;
    }
}
