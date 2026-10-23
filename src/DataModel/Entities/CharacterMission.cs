// <copyright file="CharacterMission.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

using MUnique.OpenMU.DataModel.Configuration.Missions;

/// <summary>
/// The progress of a character in a <see cref="MissionDefinition"/>: active or completed.
/// </summary>
public class CharacterMission
{
    /// <summary>
    /// Gets or sets the mission.
    /// </summary>
    public virtual MissionDefinition? Mission { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mission is active (else completed).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the index of the current step.
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    /// Gets or sets the progress of the objectives of the current step: one little-endian int per
    /// objective, in the order of the objectives.
    /// </summary>
    public byte[]? Progress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mission is shown in the tracker.
    /// </summary>
    public bool IsTracked { get; set; } = true;

    /// <summary>
    /// Gets or sets when the mission was last completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
