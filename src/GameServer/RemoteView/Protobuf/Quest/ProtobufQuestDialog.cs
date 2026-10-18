// <copyright file="ProtobufQuestDialog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Quest;

using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Builds <see cref="S2CQuestDialog"/>: the quests the opened NPC gives the own character, with their
/// state, requirements (items in the inventory, monsters killed) and rewards. The legacy client
/// kept the quest texts itself; here the server sends everything the dialog shows.
/// </summary>
internal static class ProtobufQuestDialog
{
    /// <summary>
    /// Sends the quest dialog of the opened NPC, if it gives quests to the character.
    /// </summary>
    internal static ValueTask SendAsync(ProtobufRemotePlayer player)
    {
        if (player.OpenedNpc is not { } npc || player.SelectedCharacter is not { } character)
        {
            return ValueTask.CompletedTask;
        }

        var quests = npc.Definition.Quests
            .Where(q => q.QualifiedCharacter is null || Equals(q.QualifiedCharacter, character.CharacterClass))
            .OrderBy(q => q.Group)
            .ThenBy(q => q.Number)
            .ToList();
        if (quests.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var msg = new S2CQuestDialog { NpcId = npc.Id, NpcName = npc.Definition.Designation };
        foreach (var quest in quests)
        {
            msg.Quests.Add(BuildQuest(player, character, quest, quests));
        }

        return player.SendEnvelopeAsync(new ServerEnvelope { QuestDialog = msg });
    }

    private static QuestInfo BuildQuest(Player player, Character character, QuestDefinition quest, IList<QuestDefinition> npcQuests)
    {
        var state = player.GetQuestState(quest.Group);
        var isActive = Equals(state?.ActiveQuest, quest);
        var info = new QuestInfo
        {
            Group = (uint)quest.Group,
            Number = (uint)quest.Number,
            Name = quest.Name,
            MinimumLevel = (uint)quest.MinimumCharacterLevel,
            StartCost = (uint)Math.Max(0, quest.RequiredStartMoney),
            Status = GetStatus(player, quest, state, isActive, npcQuests),
        };

        var allMet = true;
        foreach (var required in quest.RequiredItems)
        {
            var level = required.DropItemGroup?.ItemLevel;
            var count = player.Inventory?.Items.Count(i => Equals(i.Definition, required.Item) && (level is null || level == i.Level)) ?? 0;
            allMet &= count >= required.MinimumNumber;
            info.Requirements.Add(new QuestRequirement
            {
                Text = required.Item?.GetNameForLevel(level ?? 0) ?? "?",
                Current = (uint)Math.Min(count, required.MinimumNumber),
                Required = (uint)required.MinimumNumber,
            });
        }

        foreach (var kills in quest.RequiredMonsterKills)
        {
            var killed = isActive ? state!.RequirementStates.FirstOrDefault(r => Equals(r.Requirement, kills))?.KillCount ?? 0 : 0;
            allMet &= killed >= kills.MinimumNumber;
            info.Requirements.Add(new QuestRequirement
            {
                Text = $"Kill {kills.Monster?.Designation}",
                Current = (uint)Math.Min(killed, kills.MinimumNumber),
                Required = (uint)kills.MinimumNumber,
            });
        }

        info.CanComplete = isActive && allMet;
        foreach (var reward in quest.Rewards)
        {
            info.Rewards.Add(DescribeReward(reward, character));
        }

        return info;
    }

    private static QuestStatus GetStatus(Player player, QuestDefinition quest, CharacterQuestState? state, bool isActive, IList<QuestDefinition> npcQuests)
    {
        if (isActive)
        {
            return QuestStatus.Active;
        }

        var lastFinished = state?.LastFinishedQuest?.Number ?? -1;
        if (lastFinished >= quest.Number && !quest.Repeatable)
        {
            return QuestStatus.Completed;
        }

        // Quests of a group are done in order (e.g. Scroll of Emperor, then Treasures of MU).
        var previous = npcQuests.Where(q => q.Group == quest.Group && q.Number < quest.Number).Select(q => (int?)q.Number).Max();
        var previousDone = previous is null || lastFinished >= previous;
        return previousDone && state?.ActiveQuest is null && player.Level >= quest.MinimumCharacterLevel
            ? QuestStatus.Available
            : QuestStatus.Locked;
    }

    private static string DescribeReward(QuestReward reward, Character character) => reward.RewardType switch
    {
        QuestRewardType.LevelUpPoints => $"+{reward.Value} stat points",
        QuestRewardType.CharacterEvolutionFirstToSecond or QuestRewardType.CharacterEvolutionSecondToThird =>
            $"Class change: {character.CharacterClass?.NextGenerationClass?.Name ?? "next class"}",
        QuestRewardType.Experience => $"{reward.Value:N0} experience",
        QuestRewardType.Money => $"{reward.Value:N0} zen",
        QuestRewardType.Item => reward.ItemReward?.Definition?.Name.ToString() ?? "Item",
        QuestRewardType.Skill => $"Skill: {reward.SkillReward?.Name}",
        QuestRewardType.Attribute => reward.AttributeReward?.Designation ?? "Attribute",
        _ => reward.RewardType.ToString(),
    };
}
