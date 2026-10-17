// <copyright file="AdjustAreaSkillsForMobaPlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// Tunes the wizard area skills for the ground-aimed casting of the MOBA-style client.
/// The legacy values made Flame and Cometfall a one-tile column that often missed (0 hits),
/// and Evil Spirit hit everything within 7 tiles of the aimed point.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("85B268FA-8863-4DF6-87EE-1FD90C04E347")]
public class AdjustAreaSkillsForMobaPlugInSeason6 : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Adjust Area Skills For MOBA";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Flame and Cometfall hit a 2-tile radius, Ice Storm 2.5, Evil Spirit 3; Flame, Twister and Evil Spirit always hit at least once.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AdjustAreaSkillsForMobaSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        Adjust(gameConfiguration, SkillNumber.Flame, s =>
        {
            s.TargetAreaDiameter = 4;
            s.MinimumNumberOfHitsPerTarget = 1;
            s.HitChancePerDistanceMultiplier = 1;
        });
        Adjust(gameConfiguration, SkillNumber.Cometfall, s => s.TargetAreaDiameter = 4);
        Adjust(gameConfiguration, SkillNumber.IceStorm, s => s.TargetAreaDiameter = 5);
        Adjust(gameConfiguration, SkillNumber.EvilSpirit, s =>
        {
            s.EffectRange = 3;
            s.MinimumNumberOfHitsPerTarget = 1;
        });
        Adjust(gameConfiguration, SkillNumber.Twister, s => s.MinimumNumberOfHitsPerTarget = 1);
        return ValueTask.CompletedTask;
    }

    private static void Adjust(GameConfiguration gameConfiguration, SkillNumber number, Action<AreaSkillSettings> change)
    {
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)number)?.AreaSkillSettings is { } settings)
        {
            change(settings);
        }
    }
}
