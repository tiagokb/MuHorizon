// <copyright file="MissionDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration.Missions;

using MUnique.OpenMU.Annotations;

/// <summary>
/// How often a mission can be done.
/// </summary>
public enum MissionRepeat
{
    /// <summary>
    /// Only once.
    /// </summary>
    Once = 0,

    /// <summary>
    /// Once per day (server time).
    /// </summary>
    Daily = 1,

    /// <summary>
    /// Once per week (from Monday, server time).
    /// </summary>
    Weekly = 2,
}

/// <summary>
/// A mission (the new quest system): given by an NPC, made of steps with objectives, with rewards
/// for each player who completes it. Kills count for the party members around who have it too.
/// </summary>
[Cloneable]
public partial class MissionDefinition
{
    /// <summary>
    /// Gets or sets the number which identifies the mission.
    /// </summary>
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public LocalizedString Name { get; set; }

    /// <summary>
    /// Gets or sets the description (the story / what to do).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the NPC who gives the mission.
    /// </summary>
    public virtual MonsterDefinition? Giver { get; set; }

    /// <summary>
    /// Gets or sets the NPC who takes the finished mission; <c>null</c> for the <see cref="Giver"/>.
    /// </summary>
    public virtual MonsterDefinition? Receiver { get; set; }

    /// <summary>
    /// Gets or sets the minimum character level.
    /// </summary>
    public int MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum character level; 0 for none.
    /// </summary>
    public int MaximumLevel { get; set; }

    /// <summary>
    /// Gets or sets the class which can take it; <c>null</c> for every class.
    /// </summary>
    public virtual CharacterClass? QualifiedCharacter { get; set; }

    /// <summary>
    /// Gets or sets how often it can be done.
    /// </summary>
    public MissionRepeat Repeat { get; set; }

    /// <summary>
    /// Gets or sets the mission which has to be completed before; <c>null</c> for none.
    /// </summary>
    public virtual MissionDefinition? Prerequisite { get; set; }

    /// <summary>
    /// Gets or sets the steps, done in the order of <see cref="MissionStep.Index"/>.
    /// </summary>
    [MemberOfAggregate]
    public virtual ICollection<MissionStep> Steps { get; protected set; } = null!;

    /// <summary>
    /// Gets or sets the rewards, given to each player who completes it.
    /// </summary>
    [MemberOfAggregate]
    public virtual ICollection<MissionReward> Rewards { get; protected set; } = null!;

    /// <inheritdoc />
    public override string ToString() => $"{this.Number}: {this.Name}";
}
