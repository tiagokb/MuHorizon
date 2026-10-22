// <copyright file="AddMountSlotPlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the mount slot: mounts (Uniria, Dinorant, Dark Horse, Fenrir) no longer share the pet slot.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E4A9C2D7-5B31-4F86-9D0E-2C7B8A16F453")]
public class AddMountSlotPlugInSeason6 : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add Mount Slot";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Mounts get their own equipment slot, so a mount and a pet can be equipped together.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddMountSlotSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MountSlot.Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
