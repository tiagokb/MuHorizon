// <copyright file="MissionService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Missions;

using MUnique.OpenMU.DataModel.Configuration.Missions;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;

/// <summary>
/// The missions (the new quest system): accepting, progress (kills count for the party members
/// around), steps going on by themselves, handing in at the receiver NPC, rewards and repetition.
/// </summary>
public static class MissionService
{
    /// <summary>
    /// How far (tiles) party members may be from a kill to get the credit.
    /// </summary>
    private const int PartyCreditRange = 15;

    /// <summary>
    /// How far (tiles) from the receiver NPC a mission can be handed in.
    /// </summary>
    private const int TurnInRange = 6;

    /// <summary>
    /// The state of an objective of the current step.
    /// </summary>
    /// <param name="Objective">The objective.</param>
    /// <param name="Current">How many are done.</param>
    /// <param name="Required">How many are needed.</param>
    public readonly record struct ObjectiveState(MissionObjective Objective, int Current, int Required)
    {
        /// <summary>
        /// Gets a value indicating whether the objective is done.
        /// </summary>
        public bool IsDone => this.Current >= this.Required;
    }

    /// <summary>
    /// Gets the steps of the mission in order.
    /// </summary>
    /// <param name="mission">The mission.</param>
    /// <returns>The ordered steps.</returns>
    public static IReadOnlyList<MissionStep> Steps(MissionDefinition mission) => mission.Steps.OrderBy(s => s.Index).ToList();

    /// <summary>
    /// Gets the objectives of the step in a stable order (the progress bytes follow it).
    /// </summary>
    /// <param name="step">The step.</param>
    /// <returns>The ordered objectives.</returns>
    public static IReadOnlyList<MissionObjective> Objectives(MissionStep step) => step.Objectives.OrderBy(o => o.GetId()).ToList();

    /// <summary>
    /// Gets the active missions of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The active missions.</returns>
    public static IEnumerable<CharacterMission> Active(Player player) =>
        player.SelectedCharacter?.Missions.Where(m => m.IsActive && m.Mission is not null) ?? [];

    /// <summary>
    /// Gets the current step of an active mission.
    /// </summary>
    /// <param name="state">The mission state.</param>
    /// <returns>The step; <c>null</c> if the mission has none.</returns>
    public static MissionStep? CurrentStep(CharacterMission state) =>
        state.Mission is { } mission ? Steps(mission).ElementAtOrDefault(state.StepIndex) : null;

    /// <summary>
    /// Gets the state of the objectives of the current step.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="state">The mission state.</param>
    /// <returns>The objectives with their progress.</returns>
    public static IReadOnlyList<ObjectiveState> ObjectiveStates(Player player, CharacterMission state)
    {
        if (CurrentStep(state) is not { } step)
        {
            return [];
        }

        var objectives = Objectives(step);
        var result = new List<ObjectiveState>(objectives.Count);
        for (var i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            var required = Math.Max(1, objective.Count);
            var current = objective.Type == MissionObjectiveType.Collect
                ? CountItems(player, objective)
                : GetProgress(state, i);
            result.Add(new ObjectiveState(objective, Math.Min(current, required), required));
        }

        return result;
    }

    /// <summary>
    /// Gets a value indicating whether the last step is done: the mission can be handed in.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="state">The mission state.</param>
    /// <returns><c>true</c> when it can be handed in.</returns>
    public static bool IsReadyToTurnIn(Player player, CharacterMission state) =>
        state.Mission is { } mission
        && state.StepIndex == Steps(mission).Count - 1
        && ObjectiveStates(player, state).All(o => o.IsDone);

    /// <summary>
    /// Gets the NPC which takes the finished mission.
    /// </summary>
    /// <param name="mission">The mission.</param>
    /// <returns>The receiver NPC.</returns>
    public static MonsterDefinition? Receiver(MissionDefinition mission) => mission.Receiver ?? mission.Giver;

    /// <summary>
    /// Gets the text of an objective: its description, or one made from its type.
    /// </summary>
    /// <param name="objective">The objective.</param>
    /// <returns>The text.</returns>
    public static string Describe(MissionObjective objective)
    {
        if (!string.IsNullOrWhiteSpace(objective.Description))
        {
            return objective.Description;
        }

        var where = objective.Map is { } map ? $" in {map.Name}" : string.Empty;
        return objective.Type switch
        {
            MissionObjectiveType.Kill => $"Kill {objective.Count} {objective.Monster?.Designation}{where}",
            MissionObjectiveType.Collect => $"Collect {objective.Count} {objective.Item?.Name}",
            MissionObjectiveType.Talk => $"Talk to {objective.Monster?.Designation}",
            MissionObjectiveType.Reach => $"Go to {objective.Map?.Name} ({objective.X}, {objective.Y})",
            _ => objective.Type.ToString(),
        };
    }

    /// <summary>
    /// Gets the text of a reward.
    /// </summary>
    /// <param name="reward">The reward.</param>
    /// <returns>The text.</returns>
    public static string Describe(MissionReward reward) => reward.Type switch
    {
        MissionRewardType.Experience => $"{reward.Amount:N0} experience",
        MissionRewardType.Money => $"{reward.Amount:N0} zen",
        _ => $"{Math.Max(1, reward.Amount)}x {reward.Item?.Name}" + (reward.ItemLevel > 0 ? $" +{reward.ItemLevel}" : string.Empty),
    };

    /// <summary>
    /// Gets why the player can't take the mission now.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="mission">The mission.</param>
    /// <returns>The reason, or <c>null</c> if the player can take it.</returns>
    public static string? WhyNotAvailable(Player player, MissionDefinition mission)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return "No character.";
        }

        var level = player.Level;
        if (level < mission.MinimumLevel || (mission.MaximumLevel > 0 && level > mission.MaximumLevel))
        {
            return $"Needs level {mission.MinimumLevel}" + (mission.MaximumLevel > 0 ? $" to {mission.MaximumLevel}." : "+.");
        }

        if (mission.QualifiedCharacter is { } qualified && !IsOfClass(character.CharacterClass, qualified))
        {
            return $"Only for {qualified.Name}.";
        }

        if (mission.Prerequisite is { } prerequisite
            && !character.Missions.Any(m => m.Mission == prerequisite && m.CompletedAt is not null))
        {
            return $"Complete \"{prerequisite.Name}\" first.";
        }

        var state = character.Missions.FirstOrDefault(m => m.Mission == mission);
        if (state is { IsActive: true })
        {
            return "Already active.";
        }

        if (state?.CompletedAt is { } completedAt)
        {
            var now = DateTime.UtcNow;
            var available = mission.Repeat switch
            {
                MissionRepeat.Daily => completedAt < now.Date,
                MissionRepeat.Weekly => completedAt < now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)),
                _ => false,
            };
            if (!available)
            {
                return mission.Repeat == MissionRepeat.Once ? "Already completed." : $"Already done ({mission.Repeat.ToString().ToLowerInvariant()}).";
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the missions the NPC offers to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The available missions.</returns>
    public static IReadOnlyList<MissionDefinition> Offers(Player player, MonsterDefinition npc) =>
        player.GameContext.Configuration.Missions
            .Where(m => m.Giver == npc && WhyNotAvailable(player, m) is null)
            .OrderBy(m => m.Number)
            .ToList();

    /// <summary>
    /// Gets the player's missions which can be handed in at the NPC.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The missions to hand in.</returns>
    public static IReadOnlyList<MissionDefinition> TurnIns(Player player, MonsterDefinition npc) =>
        Active(player)
            .Where(m => Receiver(m.Mission!) == npc && IsReadyToTurnIn(player, m))
            .Select(m => m.Mission!)
            .ToList();

    /// <summary>
    /// The player talked to an NPC: talk objectives are done, and the NPC's missions are shown.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    public static async ValueTask OnTalkAsync(Player player, NonPlayerCharacter npc)
    {
        player.LastTalkedNpc = npc;
        foreach (var state in Active(player).ToList())
        {
            var objectives = ObjectiveStates(player, state);
            var changed = false;
            for (var i = 0; i < objectives.Count; i++)
            {
                if (objectives[i].Objective is { Type: MissionObjectiveType.Talk } talk && talk.Monster == npc.Definition && !objectives[i].IsDone)
                {
                    SetProgress(state, i, 1);
                    changed = true;
                }
            }

            if (changed)
            {
                await AdvanceAndShowAsync(player, state).ConfigureAwait(false);
            }
        }

        var offers = Offers(player, npc.Definition);
        var turnIns = TurnIns(player, npc.Definition);
        if (offers.Count > 0 || turnIns.Count > 0)
        {
            await player.InvokeViewPlugInAsync<IMissionViewPlugIn>(p => p.ShowMissionDialogAsync(npc.Definition, offers, turnIns)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A monster was killed: the kill counts for the killer and the party members around who have
    /// a matching kill objective.
    /// </summary>
    /// <param name="killer">The player who killed it.</param>
    /// <param name="monster">The monster.</param>
    public static async ValueTask OnMonsterKilledAsync(Player killer, AttackableNpcBase monster)
    {
        var players = killer.Party?.PartyList.OfType<Player>()
                          .Where(p => p == killer || (p.CurrentMap == killer.CurrentMap && p.IsInRange(monster.Position, PartyCreditRange)))
                          .ToList()
                      ?? [killer];
        foreach (var player in players)
        {
            foreach (var state in Active(player).ToList())
            {
                var objectives = ObjectiveStates(player, state);
                var changed = false;
                for (var i = 0; i < objectives.Count; i++)
                {
                    var objective = objectives[i].Objective;
                    if (objective.Type == MissionObjectiveType.Kill
                        && objective.Monster == monster.Definition
                        && (objective.Map is null || objective.Map == monster.CurrentMap.Definition)
                        && !objectives[i].IsDone)
                    {
                        SetProgress(state, i, objectives[i].Current + 1);
                        changed = true;
                    }
                }

                if (changed)
                {
                    await AdvanceAndShowAsync(player, state).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Checks the reach objectives of the player (called by the tick loop, at most once a second).
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask OnTickAsync(Player player)
    {
        if (player.NextMissionCheckUtc > DateTime.UtcNow || player.CurrentMap is null)
        {
            return;
        }

        player.NextMissionCheckUtc = DateTime.UtcNow.AddSeconds(1);
        foreach (var state in Active(player).ToList())
        {
            var objectives = ObjectiveStates(player, state);
            var changed = false;
            for (var i = 0; i < objectives.Count; i++)
            {
                if (objectives[i] is { IsDone: false, Objective: { Type: MissionObjectiveType.Reach } reach }
                    && reach.Map == player.CurrentMap.Definition
                    && player.IsInRange(new Pathfinding.Point(reach.X, reach.Y), Math.Max((byte)1, reach.Radius)))
                {
                    SetProgress(state, i, 1);
                    changed = true;
                }
            }

            if (changed)
            {
                await AdvanceAndShowAsync(player, state).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Accepts a mission offered by the NPC the player last talked to.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="number">The mission number.</param>
    public static async ValueTask AcceptAsync(Player player, short number)
    {
        if (player.SelectedCharacter is not { } character
            || player.GameContext.Configuration.Missions.FirstOrDefault(m => m.Number == number) is not { } mission)
        {
            return;
        }

        if (!IsNearNpc(player, mission.Giver))
        {
            await player.ShowBlueMessageAsync($"Talk to {mission.Giver?.Designation} to take this mission.").ConfigureAwait(false);
            return;
        }

        if (WhyNotAvailable(player, mission) is { } reason)
        {
            await player.ShowBlueMessageAsync(reason).ConfigureAwait(false);
            return;
        }

        var state = character.Missions.FirstOrDefault(m => m.Mission == mission);
        if (state is null)
        {
            state = player.PersistenceContext.CreateNew<CharacterMission>();
            state.Mission = mission;
            character.Missions.Add(state);
        }

        state.IsActive = true;
        state.StepIndex = 0;
        state.Progress = null;
        state.IsTracked = true;
        await player.ShowBlueMessageAsync($"Mission accepted: {mission.Name}").ConfigureAwait(false);
        await AdvanceAndShowAsync(player, state).ConfigureAwait(false);
    }

    /// <summary>
    /// Hands in a finished mission at its receiver NPC: collected items are taken, rewards given.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="number">The mission number.</param>
    public static async ValueTask CompleteAsync(Player player, short number)
    {
        if (Active(player).FirstOrDefault(m => m.Mission!.Number == number) is not { } state)
        {
            return;
        }

        var mission = state.Mission!;
        if (!IsNearNpc(player, Receiver(mission)))
        {
            await player.ShowBlueMessageAsync($"Hand it in to {Receiver(mission)?.Designation}.").ConfigureAwait(false);
            return;
        }

        if (!IsReadyToTurnIn(player, state))
        {
            await player.ShowBlueMessageAsync("The mission isn't finished yet.").ConfigureAwait(false);
            return;
        }

        foreach (var collect in ObjectiveStates(player, state).Where(o => o.Objective.Type == MissionObjectiveType.Collect))
        {
            await TakeItemsAsync(player, collect.Objective).ConfigureAwait(false);
        }

        state.IsActive = false;
        state.CompletedAt = DateTime.UtcNow;
        state.Progress = null;
        foreach (var reward in mission.Rewards)
        {
            await GiveRewardAsync(player, reward).ConfigureAwait(false);
        }

        await player.ShowBlueMessageAsync($"Mission completed: {mission.Name}").ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IMissionViewPlugIn>(p => p.ShowMissionRemovedAsync(mission.Number)).ConfigureAwait(false);
    }

    /// <summary>
    /// Abandons an active mission (its progress is lost).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="number">The mission number.</param>
    public static async ValueTask AbandonAsync(Player player, short number)
    {
        if (Active(player).FirstOrDefault(m => m.Mission!.Number == number) is not { } state)
        {
            return;
        }

        state.IsActive = false;
        state.Progress = null;
        state.StepIndex = 0;
        if (state.CompletedAt is null)
        {
            player.SelectedCharacter!.Missions.Remove(state);
            await player.PersistenceContext.DeleteAsync(state).ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IMissionViewPlugIn>(p => p.ShowMissionRemovedAsync(number)).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows or hides a mission in the tracker.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="number">The mission number.</param>
    /// <param name="tracked">Whether to track it.</param>
    public static async ValueTask SetTrackedAsync(Player player, short number, bool tracked)
    {
        if (Active(player).FirstOrDefault(m => m.Mission!.Number == number) is { } state)
        {
            state.IsTracked = tracked;
            await player.InvokeViewPlugInAsync<IMissionViewPlugIn>(p => p.ShowMissionUpdatedAsync(state)).ConfigureAwait(false);
        }
    }

    // Finished non-last steps go on by themselves; then the client gets the new state.
    private static async ValueTask AdvanceAndShowAsync(Player player, CharacterMission state)
    {
        var steps = Steps(state.Mission!);
        while (state.StepIndex < steps.Count - 1 && ObjectiveStates(player, state).All(o => o.IsDone))
        {
            state.StepIndex++;
            state.Progress = null;
            await player.ShowBlueMessageAsync($"{state.Mission!.Name}: {steps[state.StepIndex].Description}").ConfigureAwait(false);
        }

        if (IsReadyToTurnIn(player, state))
        {
            await player.ShowBlueMessageAsync($"{state.Mission!.Name} is done: hand it in to {Receiver(state.Mission)?.Designation}.").ConfigureAwait(false);
        }

        await player.InvokeViewPlugInAsync<IMissionViewPlugIn>(p => p.ShowMissionUpdatedAsync(state)).ConfigureAwait(false);
    }

    private static bool IsNearNpc(Player player, MonsterDefinition? npc) =>
        npc is not null
        && player.LastTalkedNpc is { } talked
        && talked.Definition == npc
        && talked.CurrentMap == player.CurrentMap
        && player.IsInRange(talked.Position, TurnInRange);

    // The qualified class or one of its later generations (e.g. a Soul Master for a Dark Wizard mission).
    private static bool IsOfClass(DataModel.Configuration.CharacterClass? characterClass, DataModel.Configuration.CharacterClass qualified)
    {
        for (var current = qualified; current is not null; current = current.NextGenerationClass == current ? null : current.NextGenerationClass)
        {
            if (current == characterClass)
            {
                return true;
            }
        }

        return false;
    }

    // Stackable items (potions, apples) count their stack (the durability); everything else counts 1.
    private static int StackOf(Item item) =>
        item.Definition is { ItemSlot: null, Durability: > 1 } ? (int)Math.Max(1, item.Durability) : 1;

    private static int GetProgress(CharacterMission state, int index) =>
        state.Progress is { } bytes && bytes.Length >= (index + 1) * 4 ? BitConverter.ToInt32(bytes, index * 4) : 0;

    private static void SetProgress(CharacterMission state, int index, int value)
    {
        var bytes = state.Progress ?? [];
        if (bytes.Length < (index + 1) * 4)
        {
            Array.Resize(ref bytes, (index + 1) * 4);
        }
        else
        {
            bytes = (byte[])bytes.Clone(); // a new array, so the change is saved
        }

        BitConverter.GetBytes(value).CopyTo(bytes, index * 4);
        state.Progress = bytes;
    }

    private static int CountItems(Player player, MissionObjective objective) =>
        player.Inventory?.Items
            .Where(i => i.ItemSlot > DataModel.InventoryConstants.LastEquippableItemSlotIndex && i.Definition == objective.Item)
            .Sum(StackOf)
        ?? 0;

    private static async ValueTask TakeItemsAsync(Player player, MissionObjective objective)
    {
        var remaining = Math.Max(1, objective.Count);
        foreach (var item in player.Inventory!.Items
                     .Where(i => i.ItemSlot > DataModel.InventoryConstants.LastEquippableItemSlotIndex && i.Definition == objective.Item)
                     .ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            var stack = StackOf(item);
            if (stack > remaining)
            {
                item.Durability -= remaining;
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
                remaining = 0;
            }
            else
            {
                remaining -= stack;
                await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask GiveRewardAsync(Player player, MissionReward reward)
    {
        switch (reward.Type)
        {
            case MissionRewardType.Experience:
                await player.AddExperienceAsync(reward.Amount, null).ConfigureAwait(false);
                break;
            case MissionRewardType.Money:
                player.TryAddMoney(reward.Amount);
                await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                break;
            case MissionRewardType.Item when reward.Item is { } definition:
                for (var n = 0; n < Math.Max(1, reward.Amount); n++)
                {
                    var item = player.PersistenceContext.CreateNew<Item>();
                    item.Definition = definition;
                    item.Level = reward.ItemLevel;
                    item.Durability = item.GetMaximumDurabilityOfOnePiece();
                    if (await player.Inventory!.AddItemAsync(item).ConfigureAwait(false))
                    {
                        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
                    }
                    else if (player.CurrentMap is { } map)
                    {
                        await map.AddAsync(new DroppedItem(item, player.Position, map, player, player.GetAsEnumerable())).ConfigureAwait(false);
                    }
                }

                break;
        }
    }
}
