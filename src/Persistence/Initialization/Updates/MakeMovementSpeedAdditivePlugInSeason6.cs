// <copyright file="MakeMovementSpeedAdditivePlugInSeason6.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes movement speed additive in season 6 game configurations.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8D222139-FBF4-4DDE-B0F7-098909E7060C")]
public class MakeMovementSpeedAdditivePlugInSeason6 : MakeMovementSpeedAdditivePlugInBase
{
    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.MakeMovementSpeedAdditiveSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
}
