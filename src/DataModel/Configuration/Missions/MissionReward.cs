// <copyright file="MissionReward.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration.Missions;

using MUnique.OpenMU.Annotations;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// What a <see cref="MissionReward"/> gives.
/// </summary>
public enum MissionRewardType
{
    /// <summary>
    /// <see cref="MissionReward.Amount"/> experience.
    /// </summary>
    Experience = 0,

    /// <summary>
    /// <see cref="MissionReward.Amount"/> zen.
    /// </summary>
    Money = 1,

    /// <summary>
    /// <see cref="MissionReward.Amount"/> times <see cref="MissionReward.Item"/>
    /// at <see cref="MissionReward.ItemLevel"/>.
    /// </summary>
    Item = 2,
}

/// <summary>
/// A reward of a <see cref="MissionDefinition"/>, for each player who completes it.
/// </summary>
[Cloneable]
public partial class MissionReward
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public MissionRewardType Type { get; set; }

    /// <summary>
    /// Gets or sets the amount (experience, zen or number of items).
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    public virtual ItemDefinition? Item { get; set; }

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public byte ItemLevel { get; set; }
}
