// <copyright file="ProtobufMiniGamePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Events;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Event (mini game) views for the Protobuf client: opening times, enter results, the running
/// event's state, the final score and the terrain changes of the event maps.
/// </summary>
internal sealed class ProtobufMiniGamePlugIn :
    IShowMiniGameOpeningStatePlugIn,
    IShowMiniGameEnterResultPlugIn,
    IUpdateMiniGameStateViewPlugIn,
    IBloodCastleStateViewPlugin,
    IChaosCastleStateViewPlugin,
    IMiniGameScoreTableViewPlugin,
    IBloodCastleScoreTableViewPlugin,
    IChangeTerrainAttributesViewPlugin
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufMiniGamePlugIn(ProtobufRemotePlayer player) => _player = player;

    private MiniGameKind CurrentKind => (MiniGameKind)(_player.CurrentMiniGame?.Definition.Type ?? MiniGameType.Undefined);

    public ValueTask ShowOpeningStateAsync(MiniGameType miniGameType, TimeSpan? timeUntilOpening, int playerCount) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            MiniGameOpening = new S2CMiniGameOpening
            {
                Kind = (MiniGameKind)miniGameType,
                SecondsUntilOpen = timeUntilOpening is { } wait ? (int)Math.Ceiling(wait.TotalSeconds) : -1,
                PlayerCount = (uint)Math.Max(0, playerCount),
            },
        });

    public ValueTask ShowResultAsync(MiniGameType miniGameType, EnterResult enterResult)
    {
        var text = enterResult switch
        {
            EnterResult.Success => null,
            EnterResult.NotOpen => "The event isn't open yet.",
            EnterResult.Full => "The event is full.",
            EnterResult.CharacterLevelTooLow => "Your level is too low for this event.",
            EnterResult.CharacterLevelTooHigh => "Your level is too high for this event.",
            EnterResult.NotEnoughMoney => "Not enough zen for the entrance fee.",
            EnterResult.PlayerKillerCantEnter => "Player killers can't enter.",
            _ => "You can't enter the event (a valid ticket is needed).",
        };
        return text is null ? ValueTask.CompletedTask : _player.ShowBlueMessageAsync(text);
    }

    public ValueTask UpdateStateAsync(MiniGameType type, MiniGameState state) =>
        this.SendStateAsync((MiniGameKind)type, state.ToString(), TimeSpan.Zero, 0, 0, null);

    public ValueTask UpdateStateAsync(BloodCastleStatus status, TimeSpan remainingTime, int maxMonster, int curMonster, IIdentifiable? questItemOwner, Item? questItem) =>
        this.SendStateAsync(MiniGameKind.BloodCastle, status.ToString(), remainingTime, maxMonster, curMonster, (questItemOwner as Player)?.Name);

    public ValueTask UpdateStateAsync(ChaosCastleStatus status, TimeSpan remainingTime, int maxMonster, int curMonster) =>
        this.SendStateAsync(MiniGameKind.ChaosCastle, status.ToString(), remainingTime, maxMonster, curMonster, null);

    public ValueTask ShowScoreTableAsync(byte playerRank, IReadOnlyCollection<(string Player, int Score, int BonusExp, int BonusMoney)> scores)
    {
        var msg = new S2CMiniGameScore { Kind = this.CurrentKind, Success = true, OwnRank = playerRank };
        foreach (var (player, score, bonusExp, bonusMoney) in scores)
        {
            msg.Rows.Add(new MiniGameScoreRow { Player = player, Score = score, BonusExp = bonusExp, BonusMoney = bonusMoney });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { MiniGameScore = msg });
    }

    public ValueTask ShowScoreTableAsync(bool success, string playerName, int totalScore, int bonusExp, int bonusMoney)
    {
        var msg = new S2CMiniGameScore { Kind = MiniGameKind.BloodCastle, Success = success, OwnRank = 1 };
        msg.Rows.Add(new MiniGameScoreRow { Player = playerName, Score = totalScore, BonusExp = bonusExp, BonusMoney = bonusMoney });
        return _player.SendEnvelopeAsync(new ServerEnvelope { MiniGameScore = msg });
    }

    public ValueTask ChangeAttributesAsync(TerrainAttributeType attribute, bool setAttribute, IReadOnlyCollection<(byte StartX, byte StartY, byte EndX, byte EndY)> areas)
    {
        var msg = new S2CTerrainChanged { Attribute = attribute.ToString(), Set = setAttribute };
        foreach (var (startX, startY, endX, endY) in areas)
        {
            msg.Areas.Add(new TerrainArea { StartX = startX, StartY = startY, EndX = endX, EndY = endY });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { TerrainChanged = msg });
    }

    private ValueTask SendStateAsync(MiniGameKind kind, string status, TimeSpan remaining, int maxMonsters, int currentMonsters, string? holder) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            MiniGameState = new S2CMiniGameState
            {
                Kind = kind,
                Status = status,
                RemainingMs = (uint)Math.Max(0, remaining.TotalMilliseconds),
                MaxMonsters = (uint)Math.Max(0, maxMonsters),
                CurrentMonsters = (uint)Math.Max(0, currentMonsters),
                QuestItemHolder = holder ?? string.Empty,
            },
        });
}
