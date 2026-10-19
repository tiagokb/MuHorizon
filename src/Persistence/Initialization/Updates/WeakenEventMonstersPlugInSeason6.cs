// <copyright file="WeakenEventMonstersPlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Weakens the monsters of the event maps (Devil Square, Blood Castle, Chaos Castle) through the
/// map's monster strength.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("6C1E8A93-2D47-4F05-B9A1-3E7D5F2C8B64")]
public class WeakenEventMonstersPlugInSeason6 : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Weaken Event Monsters";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "The monsters of Devil Square, Blood Castle and Chaos Castle get 25% of their health, defense and damage.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.WeakenEventMonstersSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        EventMonsterStrength.Apply(gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
