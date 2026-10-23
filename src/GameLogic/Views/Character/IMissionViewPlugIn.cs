// <copyright file="IMissionViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Character;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Missions;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Shows the missions (see <see cref="Missions.MissionService"/>).
/// </summary>
public interface IMissionViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows all active missions (after entering the world).
    /// </summary>
    ValueTask ShowMissionLogAsync();

    /// <summary>
    /// Shows an active mission which changed (accepted, progress, step, tracking).
    /// </summary>
    /// <param name="mission">The mission state.</param>
    ValueTask ShowMissionUpdatedAsync(CharacterMission mission);

    /// <summary>
    /// Removes a mission from the log (completed or abandoned).
    /// </summary>
    /// <param name="number">The mission number.</param>
    ValueTask ShowMissionRemovedAsync(short number);

    /// <summary>
    /// Shows what an NPC offers: missions to take and missions to hand in.
    /// </summary>
    /// <param name="npc">The NPC.</param>
    /// <param name="offers">The missions to take.</param>
    /// <param name="turnIns">The missions to hand in.</param>
    ValueTask ShowMissionDialogAsync(MonsterDefinition npc, IReadOnlyList<MissionDefinition> offers, IReadOnlyList<MissionDefinition> turnIns);
}
