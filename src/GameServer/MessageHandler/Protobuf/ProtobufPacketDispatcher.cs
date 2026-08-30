// <copyright file="ProtobufPacketDispatcher.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Protobuf;

using System.Buffers;
using System.Buffers.Binary;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Inputs;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Deserializes incoming <see cref="ClientEnvelope"/> Protobuf packets and dispatches them to
/// the appropriate game-logic actions on the owning <see cref="ProtobufRemotePlayer"/>.
/// </summary>
internal sealed class ProtobufPacketDispatcher
{
    private static readonly LoginAction LoginAction = new();
    private static readonly RequestCharacterListAction RequestCharacterListAction = new();
    private static readonly CreateCharacterAction CreateCharacterAction = new();
    private static readonly SelectCharacterAction SelectCharacterAction = new();
    private static readonly DeleteCharacterAction DeleteCharacterAction = new();
    private static readonly MoveItemAction MoveItemAction = new();
    private static readonly IncreaseStatsAction IncreaseStatsAction = new();
    private static readonly TalkNpcAction TalkNpcAction = new();
    private static readonly BuyNpcItemAction BuyNpcItemAction = new();

    private const int SkillRateLimitMs = 100;

    private readonly ProtobufRemotePlayer _player;
    private readonly ILogger<ProtobufPacketDispatcher> _logger;
    private long _lastSkillDispatchTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufPacketDispatcher"/> class.
    /// </summary>
    /// <param name="player">The player this dispatcher is attached to.</param>
    /// <param name="logger">The logger.</param>
    public ProtobufPacketDispatcher(ProtobufRemotePlayer player, ILogger<ProtobufPacketDispatcher> logger)
    {
        _player = player;
        _logger = logger;
    }

    /// <summary>
    /// Parses the raw Protobuf payload (no length prefix) and dispatches to the correct handler.
    /// </summary>
    /// <param name="sequence">The raw protobuf payload received from <see cref="Network.WebSocketConnection"/>.</param>
    public async ValueTask DispatchAsync(ReadOnlySequence<byte> sequence)
    {
        ClientEnvelope envelope;
        try
        {
            envelope = ClientEnvelope.Parser.ParseFrom(sequence.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse ClientEnvelope from {EndPoint}", _player.Connection?.EndPoint);
            return;
        }

        _logger.LogDebug("[C→S] {PayloadCase} from {EndPoint}", envelope.PayloadCase, _player.Connection?.EndPoint);

        switch (envelope.PayloadCase)
        {
            case ClientEnvelope.PayloadOneofCase.PingRequest:
                await HandlePingAsync(envelope.PingRequest).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.LoginRequest:
                await LoginAction.LoginAsync(_player, envelope.LoginRequest.Username, envelope.LoginRequest.Password).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CharacterListRequest:
                await RequestCharacterListAction.RequestCharacterListAsync(_player).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CreateCharacter:
                await CreateCharacterAction.CreateCharacterAsync(_player, envelope.CreateCharacter.Name, (int)envelope.CreateCharacter.ClassNumber).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.SelectCharacter:
                await HandleSelectCharacterAsync(envelope.SelectCharacter.Name).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.DeleteCharacter:
                await DeleteCharacterAction.DeleteCharacterAsync(_player, envelope.DeleteCharacter.Name, envelope.DeleteCharacter.SecurityCode).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.Move:
                if (_player.InputQueue.Count < 32)
                {
                    _player.InputQueue.Enqueue(new MoveInput(envelope.Move.TargetX, envelope.Move.TargetY));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.PickUpItem:
                if (_player.InputQueue.Count < 32)
                {
                    _player.InputQueue.Enqueue(new PickUpInput((ushort)envelope.PickUpItem.DropId));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.CastSkill:
                var nowTick = Environment.TickCount64;
                if (nowTick - _lastSkillDispatchTick >= SkillRateLimitMs && _player.InputQueue.Count < 32)
                {
                    _lastSkillDispatchTick = nowTick;
                    var skillId = (ushort)envelope.CastSkill.SkillId;
                    var targetId = (ushort)envelope.CastSkill.TargetId;
                    IPlayerInput input = skillId == 0
                        ? new BasicAttackInput(targetId)
                        : new SkillInput(skillId, targetId);
                    _player.InputQueue.Enqueue(input);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.MoveItem:
                await MoveItemAction.MoveItemAsync(
                    _player,
                    (byte)envelope.MoveItem.FromSlot,
                    Storages.Inventory,
                    (byte)envelope.MoveItem.ToSlot,
                    Storages.Inventory).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.AddStatPoint:
                var attr = envelope.AddStatPoint.Stat switch
                {
                    StatType.StatStrength   => Stats.BaseStrength,
                    StatType.StatAgility    => Stats.BaseAgility,
                    StatType.StatVitality   => Stats.BaseVitality,
                    StatType.StatEnergy     => Stats.BaseEnergy,
                    StatType.StatLeadership => Stats.BaseLeadership,
                    _ => null,
                };
                if (attr is not null)
                    await IncreaseStatsAction.IncreaseStatsAsync(_player, attr).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.InteractNpc:
                var npcId = (ushort)envelope.InteractNpc.NpcId;
                if (_player.CurrentMap?.GetObject(npcId) is NonPlayerCharacter npc)
                    await TalkNpcAction.TalkToNpcAsync(_player, npc).ConfigureAwait(false);
                // Non-merchant NPCs leave state in NpcDialogOpened (no client-close packet for unsupported dialogs).
                // Merchant NPCs already reset state in IShowMerchantStoreItemListPlugIn — this is a no-op for them.
                await _player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.BuyNpcItem:
                await BuyNpcItemAction.BuyItemAsync(_player, (byte)envelope.BuyNpcItem.Slot).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CloseShop:
                _player.OpenedNpc = null;
                break;

            default:
                _logger.LogDebug("Unhandled payload case {Case} from {EndPoint}", envelope.PayloadCase, _player.Connection?.EndPoint);
                break;
        }
    }

    private async ValueTask HandlePingAsync(PingRequest ping)
    {
        var serverTs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            PingResponse = new PingResponse
            {
                ClientTimestampMs = ping.TimestampMs,
                ServerTimestampMs = serverTs,
            },
        }).ConfigureAwait(false);
    }

    private async ValueTask HandleSelectCharacterAsync(string characterName)
    {
        // Look up the character first so we can send SelectCharacterResult BEFORE world entry.
        // OnPlayerEnteredWorldAsync (called inside SelectCharacterAsync) already triggers
        // ClientReadyAfterMapChangeAsync → S2CEnteredWorld, so the result must go out first.
        var character = _player.Account?.Characters.FirstOrDefault(c => c.Name.Equals(characterName));

        await _player.InvokeViewPlugInAsync<ICharacterFocusedPlugIn>(
            p => p.CharacterFocusedAsync(character!)).ConfigureAwait(false);

        if (character is not null)
        {
            await SelectCharacterAction.SelectCharacterAsync(_player, characterName).ConfigureAwait(false);
        }
    }
}
