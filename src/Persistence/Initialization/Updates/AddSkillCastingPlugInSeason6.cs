// <copyright file="AddSkillCastingPlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes Nova a charge skill (with a cooldown) and gives Cometfall, Inferno and Hellfire a cast time.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B7E4D2A1-5C93-4F06-8A1E-2D6F9C3B7E48")]
public class AddSkillCastingPlugInSeason6 : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add Skill Casting";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Nova is charged (2 s, 5 s cooldown); Cometfall, Inferno and Hellfire have a cast time (0.8, 1 and 0.6 s).";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddSkillCastingSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 17, 23, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        SkillCasting.Apply(gameConfiguration);
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.Nova) is { } nova)
        {
            nova.CooldownMilliseconds = SkillCasting.NovaCooldownMilliseconds;
        }

        return ValueTask.CompletedTask;
    }
}
