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
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Chat;
using MUnique.OpenMU.GameLogic.PlayerActions.Party;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.GameLogic.Views.Inventory;
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
    private static readonly LogoutAction LogoutAction = new();
    private static readonly CloseNpcDialogAction CloseNpcDialogAction = new();
    private static readonly QuestStartAction QuestStartAction = new();
    private static readonly ChatMessageAction ChatMessageAction = new();
    private static readonly PartyRequestAction PartyRequestAction = new();
    private static readonly PartyResponseAction PartyResponseAction = new();
    private static readonly PartyKickAction PartyKickAction = new();
    private const int MaxChatLength = 90; // legacy chat packets carry up to 90 characters
    private static readonly QuestCompletionAction QuestCompletionAction = new();
    private static readonly QuestCancelAction QuestCancelAction = new();
    private static readonly DeleteCharacterAction DeleteCharacterAction = new();
    private static readonly MoveItemAction MoveItemAction = new();
    private static readonly DropItemAction DropItemAction = new();
    private static readonly IncreaseStatsAction IncreaseStatsAction = new();
    private static readonly TalkNpcAction TalkNpcAction = new();
    private static readonly NpcBuffRequestAction NpcBuffRequestAction = new();
    private static readonly BuyNpcItemAction BuyNpcItemAction = new();
    private static readonly SellItemToNpcAction SellItemToNpcAction = new();
    private static readonly ItemConsumeAction ItemConsumeAction = new();

    // Flood guard for the input queue only; the real attack-speed limit is the speed-hack check
    // done by the inputs (ISpeedHackCheatCheckPlugIn).
    private const int SkillRateLimitMs = 100;
    private const int InteractionRangeTiles = 4;

    // Continuous coordinates of a 256x256 tile map (1 tile = 8 units).
    private const float MapSizeUnits = 256 * 8f;

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
                    var pickDropId = (ushort)envelope.PickUpItem.DropId;
                    var pickObj = _player.CurrentMap?.GetObject(pickDropId);
                    if (pickObj is not null && IsWithinInteractionRange(pickObj.Position))
                        _player.InputQueue.Enqueue(new PickUpInput(pickDropId));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.CastSkill:
                var nowTick = Environment.TickCount64;
                var rateLimitMs = nowTick - _lastSkillDispatchTick;
                if (rateLimitMs >= SkillRateLimitMs && _player.InputQueue.Count < 32)
                {
                    _lastSkillDispatchTick = nowTick;
                    var cast     = envelope.CastSkill;
                    var skillId  = (ushort)cast.SkillId;
                    var targetId = (ushort)cast.TargetId;
                    IPlayerInput? input = (skillId, cast.HasTargetPosition) switch
                    {
                        (0, _) => new BasicAttackInput(targetId),
                        (_, true) when IsOnMap(cast.TargetX, cast.TargetY)
                            => new AreaSkillInput(skillId, new Vector2F(cast.TargetX, cast.TargetY)),
                        (_, true) => null,
                        _ => new SkillInput(skillId, targetId),
                    };
                    _logger.LogDebug("[COMBAT] Dispatch  {Kind}  skillId={SkillId}  targetId={TargetId}  player={Player}",
                        input?.GetType().Name ?? "Rejected", skillId, targetId, _player.Name);
                    if (input is not null)
                        _player.InputQueue.Enqueue(input);
                }
                else if (rateLimitMs < SkillRateLimitMs)
                {
                    _logger.LogDebug("[COMBAT] Dispatch RATE-LIMITED  skillId={SkillId}  elapsed={Elapsed}ms  min={Min}ms",
                        envelope.CastSkill.SkillId, rateLimitMs, SkillRateLimitMs);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.MoveItem:
                await MoveItemAction.MoveItemAsync(
                    _player,
                    (byte)envelope.MoveItem.FromSlot,
                    (Storages)envelope.MoveItem.FromStorage,
                    (byte)envelope.MoveItem.ToSlot,
                    (Storages)envelope.MoveItem.ToStorage).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CloseVault:
                _player.Vault = null;
                _player.OpenedNpc = null;
                await _player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
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
                if (_player.CurrentMap?.GetObject(npcId) is NonPlayerCharacter npc && IsWithinInteractionRange(npc.Position))
                {
                    await TalkNpcAction.TalkToNpcAsync(_player, npc).ConfigureAwait(false);
                    await NpcBuffRequestAction.RequestBuffAsync(_player).ConfigureAwait(false);
                }
                // Vault and chaos machine NPCs must stay in NpcDialogOpened so MoveItemAction.IsStorageContextAllowed
                // passes. All other NPCs (merchants, unsupported dialogs) can return to EnteredWorld immediately.
                if (_player.OpenedNpc?.Definition.NpcWindow is not (NpcWindow.VaultStorage or NpcWindow.ChaosMachine))
                    await _player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.BuyNpcItem:
                await BuyNpcItemAction.BuyItemAsync(_player, (byte)envelope.BuyNpcItem.Slot).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CloseShop:
                _player.OpenedNpc = null;
                break;

            case ClientEnvelope.PayloadOneofCase.SellNpcItem:
                await SellItemToNpcAction.SellItemAsync(
                    _player,
                    (byte)envelope.SellNpcItem.Slot,
                    envelope.SellNpcItem.Confirmed).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.UseItem:
                var useSlot = (byte)envelope.UseItem.Slot;
                var targetSlot = envelope.UseItem.HasTargetSlot
                    ? (byte)envelope.UseItem.TargetSlot
                    : useSlot;
                await ItemConsumeAction.HandleConsumeRequestAsync(
                    _player, useSlot, targetSlot, FruitUsage.Undefined).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.DropItem:
                await DropItemAction.DropItemAsync(
                    _player,
                    (byte)envelope.DropItem.Slot,
                    _player.Position).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.CastControl:
                if (_player.InputQueue.Count < 32)
                {
                    var control = envelope.CastControl;
                    _player.InputQueue.Enqueue(new CastControlInput(
                        control.Action switch
                        {
                            CastAction.StartCharge => CastControlInput.CastControlAction.StartCharge,
                            CastAction.Release => CastControlInput.CastControlAction.Release,
                            _ => CastControlInput.CastControlAction.Cancel,
                        },
                        (ushort)control.SkillId));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.MapLoaded:
                // Only after a warp (S2CMapChanged): the player is off the map until now.
                if (_player.CurrentMap is null && _player.SelectedCharacter?.CurrentMap is not null
                    && !_player.PlayerState.CurrentState.IsDisconnectedOrFinished())
                {
                    await _player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.PartyInvite:
                if (_player.CurrentMap?.GetObject((ushort)envelope.PartyInvite.TargetId) is Player invited && invited != _player)
                {
                    await PartyRequestAction.HandlePartyRequestAsync(_player, invited).ConfigureAwait(false);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.PartyResponse:
                await PartyResponseAction.HandleResponseAsync(_player, envelope.PartyResponse.Accepted).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.PartyKick:
                await PartyKickAction.KickPlayerAsync(_player, (byte)envelope.PartyKick.Index).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.SetPvp:
                if (_player.InputQueue.Count < 32)
                {
                    _player.InputQueue.Enqueue(new PvpToggleInput(envelope.SetPvp.Enabled));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.ChatMessage:
                var chatText = envelope.ChatMessage.Text.Trim();
                if (chatText.Length is > 0 and <= MaxChatLength && _player.SelectedCharacter is { } chatCharacter)
                {
                    await ChatMessageAction.ChatMessageAsync(_player, chatCharacter.Name, chatText, false).ConfigureAwait(false);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.QuestAction:
                var questAction = envelope.QuestAction;
                var questGroup = (short)questAction.Group;
                var questNumber = (short)questAction.Number;
                switch (questAction.Action)
                {
                    case QuestActionKind.Start:
                        await QuestStartAction.StartQuestAsync(_player, questGroup, questNumber).ConfigureAwait(false);
                        break;
                    case QuestActionKind.Complete:
                        await QuestCompletionAction.CompleteQuestAsync(_player, questGroup, questNumber).ConfigureAwait(false);
                        break;
                    default:
                        await QuestCancelAction.CancelQuestAsync(_player, questGroup, questNumber).ConfigureAwait(false);
                        break;
                }

                // The dialog always shows the current state (also when the action was refused).
                await RemoteView.Protobuf.Quest.ProtobufQuestDialog.SendAsync(_player).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.ChaosMix:
                // The recipe follows from the items in the machine (S2CChaosMachine shows it).
                if (RemoteView.Protobuf.NPC.ProtobufChaosMachine.CraftAction.GetMixPreview(_player) is { } mix)
                {
                    await RemoteView.Protobuf.NPC.ProtobufChaosMachine.CraftAction.MixItemsAsync(_player, mix.Crafting.Number, 0).ConfigureAwait(false);
                }
                else
                {
                    await _player.ShowBlueMessageAsync("These items can't be mixed.").ConfigureAwait(false);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.CloseNpcDialog:
                await CloseNpcDialogAction.CloseNpcDialogAsync(_player).ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.Logout:
                if (_player.SelectedCharacter is not null)
                {
                    await LogoutAction.LogoutAsync(
                        _player,
                        envelope.Logout.Kind == LogoutKind.LoginScreen ? LogoutType.CloseGame : LogoutType.BackToCharacterSelection).ConfigureAwait(false);
                }

                break;

            case ClientEnvelope.PayloadOneofCase.GetWarpList:
                await SendWarpListAsync().ConfigureAwait(false);
                break;

            case ClientEnvelope.PayloadOneofCase.Warp:
                if (_player.InputQueue.Count < 32)
                {
                    _player.InputQueue.Enqueue(new WarpInput((int)envelope.Warp.Index));
                }

                break;

            case ClientEnvelope.PayloadOneofCase.Teleport:
                if (envelope.Teleport.TargetX <= byte.MaxValue && envelope.Teleport.TargetY <= byte.MaxValue
                    && _player.InputQueue.Count < 32)
                {
                    _player.InputQueue.Enqueue(new TeleportInput(new Pathfinding.Point(
                        (byte)envelope.Teleport.TargetX,
                        (byte)envelope.Teleport.TargetY)));
                }

                break;

            default:
                _logger.LogDebug("Unhandled payload case {Case} from {EndPoint}", envelope.PayloadCase, _player.Connection?.EndPoint);
                break;
        }
    }

    private static bool IsOnMap(float x, float y) =>
        x is >= 0 and < MapSizeUnits && y is >= 0 and < MapSizeUnits;

    private bool IsWithinInteractionRange(Pathfinding.Point other)
    {
        var p = _player.Position;
        return Math.Abs(p.X - other.X) <= InteractionRangeTiles
            && Math.Abs(p.Y - other.Y) <= InteractionRangeTiles;
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

    private ValueTask SendWarpListAsync()
    {
        var list = new S2CWarpList();
        foreach (var warp in _player.GameContext.Configuration.WarpList.Where(w => w.Gate?.Map is not null).OrderBy(w => w.Index))
        {
            list.Entries.Add(new WarpEntry
            {
                Index = (uint)warp.Index,
                Name = warp.Name.ToString(),
                Cost = (uint)Math.Max(0, warp.Costs),
                LevelRequirement = (uint)Math.Max(0, _player.SelectedCharacter?.GetEffectiveMoveLevelRequirement(warp.LevelRequirement) ?? warp.LevelRequirement),
                MapId = (uint)warp.Gate!.Map!.Number,
            });
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { WarpList = list });
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
