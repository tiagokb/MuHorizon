// <copyright file="ProtobufQuestPlugIns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Quest;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Talking to a quest NPC (Sebina, Marlon...) opens the quest dialog (S2CQuestDialog).
/// </summary>
internal sealed class ProtobufLegacyQuestStateDialogPlugIn : ILegacyQuestStateDialogPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufLegacyQuestStateDialogPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowAsync() => ProtobufQuestDialog.SendAsync(_player);
}

/// <summary>
/// A quest started: tells the player and refreshes the dialog.
/// </summary>
internal sealed class ProtobufQuestStartedPlugIn : IQuestStartedPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufQuestStartedPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask QuestStartedAsync(QuestDefinition quest)
    {
        await _player.ShowBlueMessageAsync($"Quest started: {quest.Name}").ConfigureAwait(false);
        await ProtobufQuestDialog.SendAsync(_player).ConfigureAwait(false);
    }
}

/// <summary>
/// A quest was completed: tells the player and refreshes the dialog.
/// </summary>
internal sealed class ProtobufQuestCompletionResponsePlugIn : IQuestCompletionResponsePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufQuestCompletionResponsePlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask QuestCompletedAsync(QuestDefinition quest)
    {
        await _player.ShowBlueMessageAsync($"Quest completed: {quest.Name}").ConfigureAwait(false);
        await ProtobufQuestDialog.SendAsync(_player).ConfigureAwait(false);
    }
}

/// <summary>
/// A quest reward of the own character: a message, and the updated character state
/// (stat points, and the new class after a class change).
/// </summary>
internal sealed class ProtobufLegacyQuestRewardPlugIn : ILegacyQuestRewardPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufLegacyQuestRewardPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ShowAsync(Player player, QuestRewardType rewardType, int value, AttributeDefinition? attributeReward)
    {
        if (player != _player)
        {
            return;
        }

        var text = rewardType switch
        {
            QuestRewardType.LevelUpPoints => $"You received {value} stat points.",
            QuestRewardType.CharacterEvolutionFirstToSecond or QuestRewardType.CharacterEvolutionSecondToThird =>
                $"You are now a {_player.SelectedCharacter?.CharacterClass?.Name}!",
            QuestRewardType.Attribute => $"You received: {attributeReward?.Designation}",
            _ => null,
        };
        if (text is not null)
        {
            await _player.ShowBlueMessageAsync(text).ConfigureAwait(false);
        }

        await new ProtobufUpdateCharacterStatePlugIn(_player).UpdateCharacterStatsAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// What an NPC says (e.g. "Come back with more power.") as a system message.
/// </summary>
internal sealed class ProtobufShowMessageOfObjectPlugIn : IShowMessageOfObjectPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowMessageOfObjectPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowMessageOfObjectAsync(string message, IIdentifiable sender)
    {
        string? name = sender is NonPlayerCharacter npc ? npc.Definition.Designation.ToString() : null;
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            SystemMessage = new S2CSystemMessage { Text = name is null ? message : $"{name}: {message}" },
        });
    }
}
