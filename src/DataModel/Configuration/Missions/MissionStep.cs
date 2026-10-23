// <copyright file="MissionStep.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration.Missions;

using MUnique.OpenMU.Annotations;

/// <summary>
/// A step of a <see cref="MissionDefinition"/>: done when all its objectives are done. The last
/// step is handed in at the receiver NPC; the others go on by themselves.
/// </summary>
[Cloneable]
public partial class MissionStep
{
    /// <summary>
    /// Gets or sets the position of the step in the mission (0 = first).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets what the step is about.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the objectives.
    /// </summary>
    [MemberOfAggregate]
    public virtual ICollection<MissionObjective> Objectives { get; protected set; } = null!;

    /// <inheritdoc />
    public override string ToString() => $"{this.Index}: {this.Description}";
}
