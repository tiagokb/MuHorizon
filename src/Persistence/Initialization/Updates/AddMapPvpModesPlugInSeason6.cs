// <copyright file="AddMapPvpModesPlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the PvP modes of the maps: towns (Lorencia, Devias, Noria, Elbeland) PvE only, the rest
/// "flagged" — players who turned PvP on can fight each other.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("D4A6E1F2-7B38-4C95-8E0A-6F2B9C1D3E57")]
public class AddMapPvpModesPlugInSeason6 : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add Map PvP Modes";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Towns are PvE only; on the other maps players who turned PvP on can fight each other.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddMapPvpModesSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MapPvpModes.Apply(gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
