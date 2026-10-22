// <copyright file="ProtobufGuildPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Guild;

using System.Collections.Concurrent;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Guild;
using MUnique.OpenMU.GameLogic.Views.Guild;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Guild views for the Protobuf client: the creation dialog (Guild Master NPC), join requests,
/// the member list, the guild shown above players, and results as messages.
/// </summary>
internal sealed class ProtobufGuildPlugIn :
    IShowGuildMasterDialogPlugIn,
    IShowGuildCreationDialogPlugIn,
    IShowGuildCreateResultPlugIn,
    IShowGuildJoinRequestPlugIn,
    IGuildJoinResponsePlugIn,
    IGuildKickResultPlugIn,
    IShowGuildListPlugIn,
    IAssignPlayersToGuildPlugIn,
    IPlayerLeftGuildPlugIn,
    IShowGuildInfoPlugIn
{
    private static readonly GuildListRequestAction ListAction = new();

    // Guild names by id: guilds don't rename, so they're fetched once per server run.
    private static readonly ConcurrentDictionary<uint, string> GuildNames = new();

    private readonly ProtobufRemotePlayer _player;

    public ProtobufGuildPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowGuildMasterDialogAsync() => this.ShowCreateDialogAsync();

    public ValueTask ShowGuildCreationDialogAsync() => this.ShowCreateDialogAsync();

    public ValueTask ShowGuildCreateResultAsync(GuildCreateErrorDetail errorDetail) =>
        _player.ShowBlueMessageAsync(errorDetail == GuildCreateErrorDetail.None ? "Guild created." : "A guild with this name already exists.");

    public ValueTask ShowGuildJoinRequestAsync(Player requester) =>
        _player.SendEnvelopeAsync(new ServerEnvelope
        {
            GuildJoinRequest = new S2CGuildJoinRequest { RequesterName = requester.Name },
        });

    public ValueTask ShowGuildJoinResponseAsync(GuildRequestAnswerResult response)
    {
        var text = response switch
        {
            GuildRequestAnswerResult.Accepted => "You joined the guild.",
            GuildRequestAnswerResult.Refused => "The guild master refused your request.",
            GuildRequestAnswerResult.GuildFull => "The guild is full.",
            GuildRequestAnswerResult.Disconnected => "The guild master is offline.",
            GuildRequestAnswerResult.NotTheGuildMaster => "This player is not a guild master.",
            GuildRequestAnswerResult.AlreadyHaveGuild => "You are already in a guild.",
            GuildRequestAnswerResult.GuildMasterOrRequesterIsBusy => "The guild master is busy.",
            GuildRequestAnswerResult.MinimumLevel6 => "You need at least level 6 to join a guild.",
            _ => null,
        };
        return text is null ? ValueTask.CompletedTask : _player.ShowBlueMessageAsync(text);
    }

    public async ValueTask GuildKickResultAsync(GuildKickSuccess successCode)
    {
        var text = successCode switch
        {
            GuildKickSuccess.KickSucceeded => "You are no longer in the guild.", // left, kicked or the guild was disbanded
            GuildKickSuccess.GuildDisband => "The guild was disbanded.",
            GuildKickSuccess.FailedBecausePlayerIsNotGuildMaster => "Only the guild master can kick members.",
            _ => "That didn't work.",
        };
        await _player.ShowBlueMessageAsync(text).ConfigureAwait(false);
        if (_player.GuildStatus is not null)
        {
            // The kicked member left: the master's list changed.
            await ListAction.RequestGuildListAsync(_player).ConfigureAwait(false);
        }
    }

    public ValueTask ShowGuildListAsync(IReadOnlyCollection<GuildListEntry> players, Interfaces.Guild guild)
    {
        var msg = new S2CGuildMembers
        {
            GuildName = guild.Name ?? string.Empty,
            OwnRole = (GuildRole)(_player.GuildStatus?.Position ?? GuildPosition.Undefined),
        };
        foreach (var entry in players.OrderBy(e => e.PlayerPosition == GuildPosition.GuildMaster ? 0 : 1).ThenBy(e => e.PlayerName))
        {
            msg.Members.Add(new GuildMemberData
            {
                Name = entry.PlayerName ?? string.Empty,
                Role = (GuildRole)entry.PlayerPosition,
                Online = entry.ServerId != 0xFF,
            });
        }

        if (guild.Name is { } name && _player.GuildStatus is { } status)
        {
            GuildNames[status.GuildId] = name;
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { GuildMembers = msg });
    }

    public async ValueTask AssignPlayerToGuildAsync(Player guildPlayer, bool appearsNew)
    {
        await this.SendEntityGuildAsync(guildPlayer).ConfigureAwait(false);
        if (guildPlayer == _player)
        {
            await ListAction.RequestGuildListAsync(_player).ConfigureAwait(false);
        }
    }

    public async ValueTask AssignPlayersToGuildAsync(ICollection<Player> guildPlayers, bool appearsNew)
    {
        foreach (var guildPlayer in guildPlayers)
        {
            await this.AssignPlayerToGuildAsync(guildPlayer, appearsNew).ConfigureAwait(false);
        }
    }

    public async ValueTask PlayerLeftGuildAsync(Player player)
    {
        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EntityGuild = new S2CEntityGuild { EntityId = player.Id },
        }).ConfigureAwait(false);
        if (player == _player)
        {
            await _player.SendEnvelopeAsync(new ServerEnvelope { GuildMembers = new S2CGuildMembers() }).ConfigureAwait(false);
        }
    }

    // The client gets the guild name with S2CEntityGuild, so it never asks for guild info.
    public ValueTask ShowGuildInfoAsync(uint guildId) => ValueTask.CompletedTask;

    /// <summary>
    /// Sends the guild of a player (shown above it); used when players come into scope.
    /// </summary>
    internal async ValueTask SendEntityGuildAsync(Player guildPlayer)
    {
        if (guildPlayer.GuildStatus is not { } status)
        {
            return;
        }

        var name = await this.GetGuildNameAsync(status.GuildId).ConfigureAwait(false);
        await _player.SendEnvelopeAsync(new ServerEnvelope
        {
            EntityGuild = new S2CEntityGuild { EntityId = guildPlayer.Id, GuildName = name, Role = (GuildRole)status.Position },
        }).ConfigureAwait(false);
    }

    private async ValueTask<string> GetGuildNameAsync(uint guildId)
    {
        if (GuildNames.TryGetValue(guildId, out var name))
        {
            return name;
        }

        if ((_player.GameContext as IGameServerContext)?.GuildServer is { } guildServer
            && await guildServer.GetGuildAsync(guildId).ConfigureAwait(false) is { Name: { } guildName })
        {
            GuildNames[guildId] = guildName;
            return guildName;
        }

        return string.Empty;
    }

    private ValueTask ShowCreateDialogAsync() =>
        _player.SendEnvelopeAsync(new ServerEnvelope { GuildCreateDialog = new S2CGuildCreateDialog() });
}
