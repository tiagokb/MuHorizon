// <copyright file="MissionObjective.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration.Missions;

using MUnique.OpenMU.Annotations;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// What a <see cref="MissionObjective"/> asks for.
/// </summary>
public enum MissionObjectiveType
{
    /// <summary>
    /// Kill <see cref="MissionObjective.Count"/> of <see cref="MissionObjective.Monster"/>
    /// (on <see cref="MissionObjective.Map"/>, if set). Party kills count.
    /// </summary>
    Kill = 0,

    /// <summary>
    /// Have <see cref="MissionObjective.Count"/> of <see cref="MissionObjective.Item"/> in the
    /// inventory; they're taken when the mission is handed in.
    /// </summary>
    Collect = 1,

    /// <summary>
    /// Talk to the NPC <see cref="MissionObjective.Monster"/>.
    /// </summary>
    Talk = 2,

    /// <summary>
    /// Reach <see cref="MissionObjective.X"/>/<see cref="MissionObjective.Y"/> (within
    /// <see cref="MissionObjective.Radius"/>) on <see cref="MissionObjective.Map"/>.
    /// </summary>
    Reach = 3,
}

/// <summary>
/// An objective of a <see cref="MissionStep"/>.
/// </summary>
[Cloneable]
public partial class MissionObjective
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public MissionObjectiveType Type { get; set; }

    /// <summary>
    /// Gets or sets the text shown in the mission log; generated from the type if empty.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the monster to kill or the NPC to talk to.
    /// </summary>
    public virtual MonsterDefinition? Monster { get; set; }

    /// <summary>
    /// Gets or sets the item to collect.
    /// </summary>
    public virtual ItemDefinition? Item { get; set; }

    /// <summary>
    /// Gets or sets the map (where to kill, or where to go).
    /// </summary>
    public virtual GameMapDefinition? Map { get; set; }

    /// <summary>
    /// Gets or sets the x coordinate to reach.
    /// </summary>
    public byte X { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate to reach.
    /// </summary>
    public byte Y { get; set; }

    /// <summary>
    /// Gets or sets how close to <see cref="X"/>/<see cref="Y"/> counts as reached.
    /// </summary>
    public byte Radius { get; set; }

    /// <summary>
    /// Gets or sets how many (kills, items); 1 for talk and reach.
    /// </summary>
    public int Count { get; set; } = 1;
}
