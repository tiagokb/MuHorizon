// <copyright file="GuildChangeRoleAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Guild;

using MUnique.OpenMU.GameLogic.Views.Guild;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The guild master gives a member a role: normal member, assistant master or battle master.
/// The member has to be online (the guild server changes roles by character id).
/// </summary>
public class GuildChangeRoleAction
{
    private static readonly GuildListRequestAction ListAction = new();

    /// <summary>
    /// Changes the role of the guild member.
    /// </summary>
    /// <param name="player">The guild master.</param>
    /// <param name="memberName">The name of the member.</param>
    /// <param name="role">The new role.</param>
    public async ValueTask ChangeRoleAsync(Player player, string memberName, GuildPosition role)
    {
        if (player.GuildStatus is not { Position: GuildPosition.GuildMaster } status)
        {
            await player.ShowBlueMessageAsync("Only the guild master can change roles.").ConfigureAwait(false);
            return;
        }

        if (role is not (GuildPosition.NormalMember or GuildPosition.AssistantMaster or GuildPosition.BattleMaster))
        {
            return;
        }

        var member = player.GameContext.GetPlayerByCharacterName(memberName);
        if (member?.GuildStatus?.GuildId != status.GuildId || member == player || member.SelectedCharacter is null)
        {
            await player.ShowBlueMessageAsync($"{memberName} has to be an online member of your guild.").ConfigureAwait(false);
            return;
        }

        if ((player.GameContext as IGameServerContext)?.GuildServer is not { } guildServer)
        {
            return;
        }

        await guildServer.ChangeGuildMemberPositionAsync(status.GuildId, member.SelectedCharacter.Id, role).ConfigureAwait(false);
        member.GuildStatus = new GuildMemberStatus(status.GuildId, role);
        await member.ShowBlueMessageAsync($"You are now {RoleName(role)} of your guild.").ConfigureAwait(false);

        foreach (var guildPlayer in await player.GameContext.GetPlayersAsync().ConfigureAwait(false))
        {
            if (guildPlayer.GuildStatus?.GuildId == status.GuildId)
            {
                await ListAction.RequestGuildListAsync(guildPlayer).ConfigureAwait(false);
            }
        }

        await member.ForEachWorldObserverAsync<IAssignPlayersToGuildPlugIn>(p => p.AssignPlayerToGuildAsync(member, false), true).ConfigureAwait(false);
    }

    private static string RoleName(GuildPosition role) => role switch
    {
        GuildPosition.AssistantMaster => "assistant master",
        GuildPosition.BattleMaster => "battle master",
        _ => "a member",
    };
}
