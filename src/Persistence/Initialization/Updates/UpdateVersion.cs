// <copyright file="UpdateVersion.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

/// <summary>
/// Enum which keeps track of the <see cref="IConfigurationUpdatePlugIn.Version"/>,
/// so that it's easier to know which number is next.
/// </summary>
public enum UpdateVersion
{
    /// <summary>
    /// Undefined version.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// The version of the <see cref="ChaosCastleDataUpdatePlugIn"/>.
    /// </summary>
    ChaosCastleDataUpdate = 1,

    /// <summary>
    /// The version of the <see cref="SystemConfigurationAddedPlugInSeason6"/>.
    /// </summary>
    SystemConfigurationAddedSeason6 = 2,

    /// <summary>
    /// The version of the <see cref="SpawnFixesUpdatePlugIn"/>.
    /// </summary>
    SpawnFixesUpdate = 5,

    /// <summary>
    /// The version of the <see cref="FixLevelDiv20ExcOptionUpdatePlugIn"/>.
    /// </summary>
    FixLevelDiv20ExcOptionUpdate = 6,

    /// <summary>
    /// The version of the <see cref="FixSetBonusesPlugIn.Season6"/>.
    /// </summary>
    FixSetBonusesSeason6 = 7,

    /// <summary>
    /// The version of the <see cref="FixWarpLevelUpdatePlugIn"/>.
    /// </summary>
    FixWarpLevelUpdate = 10,

    /// <summary>
    /// The version of the <see cref="AddQuestItemLimitPlugIn"/>.
    /// </summary>
    AddQuestItemLimit = 11,

    /// <summary>
    /// The version of the <see cref="AddGuardsDataPlugIn"/>.
    /// </summary>
    AddGuardsData = 12,

    /// <summary>
    /// The version of the <see cref="FixWarriorMorningStarPlugIn"/>.
    /// </summary>
    FixWarriorMorningStar = 13,

    /// <summary>
    /// The version of the <see cref="InfinityArrowSkillOnQuestCompletionPlugIn"/>.
    /// </summary>
    InfinityArrowSkillOnQuestCompletion = 15,

    /// <summary>
    /// The version of the <see cref="AddPointsPerResetAttributePlugIn"/>.
    /// </summary>
    AddPointsPerResetByClassAttribute = 16,

    /// <summary>
    /// The version of the <see cref="AddKalimaPlugIn"/>.
    /// </summary>
    AddKalima = 17,

    /// <summary>
    /// The version of the <see cref="AddDuelConfigurationPlugIn"/>.
    /// </summary>
    AddDuelConfiguration = 18,

    /// <summary>
    /// The version of the <see cref="ChainLightningUpdatePlugIn"/>.
    /// </summary>
    ChainLightningUpdate = 19,

    /// <summary>
    /// The version of the <see cref="WizEnhanceAndRagefighterSkillsUpdatePlugIn"/>.
    /// </summary>
    WizEnhanceAndRagefighterSkills = 20,

    /// <summary>
    /// The version of the <see cref="FixIgnoreDefenseSkillUpdatePlugIn"/>.
    /// </summary>
    FixIgnoreDefenseSkill = 21,

    /// <summary>
    /// The version of the <see cref="FixWingsAndCapesCraftingsUpdatePlugIn"/>.
    /// </summary>
    FixWingsAndCapesCraftings = 22,

    /// <summary>
    /// The version of the <see cref="FixDamageAbsorbItemsUpdatePlugIn"/>.
    /// </summary>
    FixDamageAbsorbItems = 23,

    /// <summary>
    /// The version of the <see cref="FixSocketSeedCraftingUpdatePlugIn"/>.
    /// </summary>
    FixSocketSeedCrafting = 24,

    /// <summary>
    /// The version of the <see cref="FixLifeSwellEffectUpdatePlugIn"/>.
    /// </summary>
    FixLifeSwellEffect = 25,

    /// <summary>
    /// The version of the <see cref="FixAncientDiscriminatorsUpdatePlugIn"/>.
    /// </summary>
    FixAncientDiscriminators = 26,

    /// <summary>
    /// The version of the <see cref="FixDrainLifeSkillUpdate"/>.
    /// </summary>
    FixDrainLifeSkill = 27,

    /// <summary>
    /// The version of the <see cref="AddItemDropGroupForJewelsUpdateSeason6"/>.
    /// </summary>
    AddItemDropGroupForJewelsSeason6 = 30,

    /// <summary>
    /// The version of the <see cref="FixMaxManaAndAbilityJewelryOptionsUpdateSeason6"/>.
    /// </summary>
    FixMaxManaAndAbilityJewelryOptionsSeason6 = 31,

    /// <summary>
    /// The version of the <see cref="FixWingsDmgRatesUpdatePlugInSeason6"/>.
    /// </summary>
    FixWingsDmgRatesPlugInSeason6 = 34,

    /// <summary>
    /// The version of the <see cref="AddHarmonyOptionWeightsUpdateSeason6"/>.
    /// </summary>
    AddHarmonyOptionWeightsSeason6 = 35,

    /// <summary>
    /// The version of the <see cref="FixDuelArenaSafezoneMapUpdate"/>.
    /// </summary>
    FixDuelArenaSafezoneMap = 36,

    /// <summary>
    /// The version of the <see cref="FixAttackSpeedCalculationUpdate"/>.
    /// </summary>
    FixAttackSpeedCalculation = 37,

    /// <summary>
    /// The version of the <see cref="AddAreaSkillSettingsUpdatePlugIn"/>.
    /// </summary>
    AddAreaSkillSettings = 38,

    /// <summary>
    /// The version of the <see cref="FixItemRequirementsPlugIn"/>.
    /// </summary>
    FixItemRequirements = 39,

    /// <summary>
    /// The version of the <see cref="FixWingsDmgRatesUpdatePlugInSeason6"/>.
    /// </summary>
    FixWeaponRisePercentageSeason6 = 42,

    /// <summary>
    /// The version of the <see cref="FixChaosMixesPlugInSeason6"/>.
    /// </summary>
    FixChaosMixesSeason6 = 45,

    /// <summary>
    /// The version of the <see cref="FixItemOptionsAndAttackSpeedPlugInSeason6"/>.
    /// </summary>
    FixItemOptionsAndAttackSpeedSeason6 = 48,

    /// <summary>
    /// The version of the <see cref="Updates.FixHorseFenrirOptionsSoulBarrierPlugIn"/>.
    /// </summary>
    FixHorseFenrirOptionsSoulBarrierPlugIn = 49,

    /// <summary>
    /// The version of the <see cref="FixCharStatsForceWavePlugInSeason6"/>.
    /// </summary>
    FixCharStatsForceWaveSeason6 = 52,

    /// <summary>
    /// The version of the <see cref="FixDefenseCalcsPlugInSeason6"/>.
    /// </summary>
    FixDefenseCalcsSeason6 = 55,

    /// <summary>
    /// The version of the <see cref="FixDamageCalcsPlugInSeason6"/>.
    /// </summary>
    FixDamageCalcsSeason6 = 58,

    /// <summary>
    /// The version of the <see cref="FixEventItemsDropFromMonstersUpdatePlugInSeason6"/>.
    /// </summary>
    FixEventItemsDropFromMonstersSeason6 = 59,

    /// <summary>
    /// The version of the <see cref="FixItemRequirementsPlugIn2"/>.
    /// </summary>
    FixItemRequirements2 = 61,

    /// <summary>
    /// The version of the <see cref="FixJeweleryPetsDamageCalcsPlugInSeason6"/>.
    /// </summary>
    FixJeweleryPetsDamageCalcsSeason6 = 64,

    /// <summary>
    /// The version of the <see cref="FixSkillMultipliersPlugIn"/>.
    /// </summary>
    FixSkillMultipliers = 65,

    /// <summary>
    /// The version of the <see cref="AddSummonerBuffSkillsPlugIn"/>.
    /// </summary>
    AddSummonerBuffSkills = 66,

    /// <summary>
    /// The version of the <see cref="AddProjectileCountToTripleShotUpdatePlugIn"/>.
    /// </summary>
    AddProjectileCountToTripleShot = 67,

    /// <summary>
    /// The version of the <see cref="RemoveJewelDropLevelGapPlugInSeason6"/>.
    /// </summary>
    RemoveJewelDropLevelGapSeason6 = 70,

    /// <summary>
    /// The version of the <see cref="FixRageFighterMultipleHitSkillsPlugIn"/>.
    /// </summary>
    FixRageFighterMultipleHitSkills = 71,

    /// <summary>
    /// The version of the <see cref="AddCrestOfMonarchDropGroupUpdateSeason6"/>.
    /// </summary>
    AddCrestOfMonarchDropGroupSeason6 = 72,

    /// <summary>
    /// The version of the <see cref="AddGlobalMoneyAmountRateAttributePlugInSeason6"/>.
    /// </summary>
    AddGlobalMoneyAmountRateAttributeSeason6 = 75,

    /// <summary>
    /// The version of the <see cref="AddMaximumAllianceSizeUpdatePlugInSeason6"/>.
    /// </summary>
    AddMaximumAllianceSizeSeason6 = 76,

    /// <summary>
    /// The version of the <see cref="FixSummonerCurseSkillsPlugIn"/>.
    /// </summary>
    FixSummonerCurseSkills = 77,

    /// <summary>
    /// The version of the <see cref="FixAreaSkillsUpdatePlugIn"/>.
    /// </summary>
    FixAreaSkills = 78,

    /// <summary>
    /// The version of the <see cref="FinishDarkLordMasterTreePlugIn"/>.
    /// </summary>
    FinishDarkLordMasterTree = 79,

    /// <summary>
    /// The version of the <see cref="FixBloodCastleMonsterAttributesUpdatePlugIn"/>.
    /// </summary>
    FixBloodCastleMonsterAttributes = 80,

    /// <summary>
    /// The version of the <see cref="AddRandomExperienceConfigAttributesPlugInSeason6"/>.
    /// </summary>
    AddRandomExperienceConfigAttributesSeason6 = 83,

    /// <summary>
    /// The version of the <see cref="AddMovementSpeedAttributesPlugInSeason6"/>.
    /// </summary>
    AddMovementSpeedAttributesSeason6 = 86,

    /// <summary>
    /// The version of the <see cref="AddMissingMerchantStoresPlugIn"/>.
    /// </summary>
    AddMissingMerchantStores = 87,

    /// <summary>
    /// The version of the <see cref="FinishDarkKnightMasterTreePlugInSeason6"/>.
    /// </summary>
    FinishDarkKnightMasterTreeSeason6 = 90,

    /// <summary>
    /// The version of the <see cref="AddWhiteWizardInvasionMobsUpdatePlugIn"/>.
    /// </summary>
    AddWhiteWizardInvasionMobs = 91,

    /// <summary>
    /// The version of the <see cref="LimitWhiteWizardDropsUpdatePlugIn"/>.
    /// </summary>
    LimitWhiteWizardDrops = 92,

    /// <summary>
    /// The version of the <see cref="AddLorenMarketJuliaWarpPlugIn"/>.
    /// </summary>
    AddLorenMarketJuliaWarp = 93,

    /// <summary>
    /// The version of the <see cref="AddIsQuestItemFlagPlugIn"/>.
    /// </summary>
    AddIsQuestItemFlag = 94,
    
    /// <summary>
    /// The version of the <see cref="AddElfSoldierBuffPlugIn"/>.
    /// </summary>
    AddElfSoldierBuff = 95,
    
    /// <summary>
    /// The version of the <see cref="FinishDarkWizardMasterTreePlugIn"/>.
    /// </summary>
    FinishDarkWizardMasterTree = 96,

    /// <summary>
    /// The version of the <see cref="MakeMovementSpeedAdditivePlugInSeason6"/>.
    /// </summary>
    MakeMovementSpeedAdditiveSeason6 = 99,

    /// <summary>
    /// The version of the <see cref="AdjustAreaSkillsForMobaPlugInSeason6"/>.
    /// </summary>
    AdjustAreaSkillsForMobaSeason6 = 100,

    /// <summary>
    /// The version of the <see cref="AddSkillCooldownsPlugInSeason6"/>.
    /// </summary>
    AddSkillCooldownsSeason6 = 101,

    /// <summary>
    /// The version of the <see cref="AddSkillCastingPlugInSeason6"/>.
    /// </summary>
    AddSkillCastingSeason6 = 102,

    /// <summary>
    /// The version of the <see cref="AddMapPvpModesPlugInSeason6"/>.
    /// </summary>
    AddMapPvpModesSeason6 = 103,
}