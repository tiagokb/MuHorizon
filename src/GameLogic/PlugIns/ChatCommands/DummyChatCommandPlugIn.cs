// <copyright file="DummyChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Dummies;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Spawns test dummies next to the game master: "/dummy punchbag", "/dummy stun", "/dummy slow", "/dummy root", "/dummy clear".
/// </summary>
[Guid("6C1D8F3A-92B4-4E57-A0C8-3B7D51E9F214")]
[PlugIn]
[Display(Name = "Dummy chat command", Description = "Spawns scripted test players next to the game master: /dummy punchbag|stun|slow|root|trade|clear.")]
[ChatCommandHelp(Command, typeof(DummyChatCommandArgs), CharacterStatus.GameMaster)]
public class DummyChatCommandPlugIn : ChatCommandPlugInBase<DummyChatCommandArgs>
{
    private const string Command = "/dummy";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player sender, DummyChatCommandArgs arguments)
    {
        var action = arguments.Behavior?.Trim().ToLowerInvariant();
        if (action == "clear")
        {
            var removed = await DummyManager.ClearAsync(sender).ConfigureAwait(false);
            await sender.ShowBlueMessageAsync($"Removed {removed} dummies.").ConfigureAwait(false);
            return;
        }

        if (!Enum.TryParse<DummyBehavior>(action, true, out var behavior))
        {
            await sender.ShowBlueMessageAsync("Usage: /dummy punchbag | stun | slow | root | trade | guild | clear").ConfigureAwait(false);
            return;
        }

        var dummy = await DummyManager.SpawnAsync(sender, behavior).ConfigureAwait(false);
        await sender.ShowBlueMessageAsync(dummy is null
            ? "No free test account (test1..test9) for a dummy."
            : $"Dummy '{dummy.Name}' ({behavior}) spawned.").ConfigureAwait(false);
    }
}
