// <copyright file="TestMissions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Missions;

/// <summary>
/// A few missions in Lorencia to try the mission system (Leo The Helper gives them).
/// </summary>
internal static class TestMissions
{
    /// <summary>
    /// Adds the test missions, if missing.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Missions.Any())
        {
            return;
        }

        var leo = Npc(gameConfiguration, 371);
        var hanzo = Npc(gameConfiguration, 251);
        var pasi = Npc(gameConfiguration, 254);
        var lorencia = gameConfiguration.Maps.First(m => m.Number == 0);
        var apple = gameConfiguration.Items.First(i => i.Group == 14 && i.Number == 0);
        var bless = gameConfiguration.Items.First(i => i.Group == 14 && i.Number == 13);

        // 1: two steps, kills (party kills count) and a place to reach.
        var pest = Mission(context, gameConfiguration, 1, "Pest Control", "Spiders and Budge Dragons are bothering the travellers. Thin them out and check the east road.", leo);
        var kills = Step(context, pest, 0, "Hunt the pests around Lorencia.");
        Kill(context, kills, Npc(gameConfiguration, 3), 10, lorencia);
        Kill(context, kills, Npc(gameConfiguration, 2), 5, lorencia);
        var scout = Step(context, pest, 1, "Check the road near the Golden Archer.");
        Reach(context, scout, lorencia, 175, 120, 5);
        Reward(context, pest, MissionRewardType.Experience, 5000);
        Reward(context, pest, MissionRewardType.Money, 50000);

        // 2: needs 1; collect items, talk to an NPC, hand in to another.
        var word = Mission(context, gameConfiguration, 2, "A Word with Hanzo", "Hanzo needs apples, and Pasi has a message for him.", leo);
        word.Receiver = hanzo;
        word.Prerequisite = pest;
        var gather = Step(context, word, 0, "Gather apples.");
        Collect(context, gather, apple, 3);
        var talk = Step(context, word, 1, "Get Pasi's message.");
        Talk(context, talk, pasi);
        Reward(context, word, MissionRewardType.Experience, 10000);
        Reward(context, word, MissionRewardType.Item, 1, bless);

        // 3: daily.
        var daily = Mission(context, gameConfiguration, 3, "Daily Hunt", "The guards pay for every Bull Fighter.", leo);
        daily.Repeat = MissionRepeat.Daily;
        var hunt = Step(context, daily, 0, "Hunt Bull Fighters.");
        Kill(context, hunt, Npc(gameConfiguration, 0), 20, lorencia);
        Reward(context, daily, MissionRewardType.Money, 100000);
    }

    private static MonsterDefinition Npc(GameConfiguration gameConfiguration, short number) =>
        gameConfiguration.Monsters.First(m => m.Number == number);

    private static MissionDefinition Mission(IContext context, GameConfiguration gameConfiguration, short number, string name, string description, MonsterDefinition giver)
    {
        var mission = context.CreateNew<MissionDefinition>();
        mission.Number = number;
        mission.Name = name;
        mission.Description = description;
        mission.Giver = giver;
        mission.MinimumLevel = 1;
        gameConfiguration.Missions.Add(mission);
        return mission;
    }

    private static MissionStep Step(IContext context, MissionDefinition mission, int index, string description)
    {
        var step = context.CreateNew<MissionStep>();
        step.Index = index;
        step.Description = description;
        mission.Steps.Add(step);
        return step;
    }

    private static MissionObjective Objective(IContext context, MissionStep step, MissionObjectiveType type)
    {
        var objective = context.CreateNew<MissionObjective>();
        objective.Type = type;
        objective.Count = 1;
        step.Objectives.Add(objective);
        return objective;
    }

    private static void Kill(IContext context, MissionStep step, MonsterDefinition monster, int count, GameMapDefinition map)
    {
        var objective = Objective(context, step, MissionObjectiveType.Kill);
        objective.Monster = monster;
        objective.Count = count;
        objective.Map = map;
    }

    private static void Collect(IContext context, MissionStep step, DataModel.Configuration.Items.ItemDefinition item, int count)
    {
        var objective = Objective(context, step, MissionObjectiveType.Collect);
        objective.Item = item;
        objective.Count = count;
    }

    private static void Talk(IContext context, MissionStep step, MonsterDefinition npc)
    {
        Objective(context, step, MissionObjectiveType.Talk).Monster = npc;
    }

    private static void Reach(IContext context, MissionStep step, GameMapDefinition map, byte x, byte y, byte radius)
    {
        var objective = Objective(context, step, MissionObjectiveType.Reach);
        objective.Map = map;
        objective.X = x;
        objective.Y = y;
        objective.Radius = radius;
    }

    private static void Reward(IContext context, MissionDefinition mission, MissionRewardType type, int amount, DataModel.Configuration.Items.ItemDefinition? item = null)
    {
        var reward = context.CreateNew<MissionReward>();
        reward.Type = type;
        reward.Amount = amount;
        reward.Item = item;
        mission.Rewards.Add(reward);
    }
}
