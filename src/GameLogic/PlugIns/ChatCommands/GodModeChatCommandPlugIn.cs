// <copyright file="GodModeChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// "/god": toggles the game master's immunity to damage, to test mechanics (events, monsters)
/// without dying and without changing balance or the character.
/// </summary>
[Guid("9E2B4C71-3A85-4D06-B1F7-58C2E0A6D394")]
[PlugIn]
[Display(Name = "God mode chat command", Description = "Toggles the game master's immunity to damage: /god.")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class GodModeChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/god";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        player.IsGodMode = !player.IsGodMode;
        await player.ShowBlueMessageAsync(player.IsGodMode ? "God mode ON: you take no damage." : "God mode OFF.").ConfigureAwait(false);
    }
}
