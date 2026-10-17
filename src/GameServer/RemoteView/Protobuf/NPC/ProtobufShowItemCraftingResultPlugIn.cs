// <copyright file="ProtobufShowItemCraftingResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Tells the player how a mix went (S2CSystemMessage); the machine's new contents follow
/// through <see cref="ProtobufShowMerchantStoreItemListPlugIn"/>.
/// </summary>
internal sealed class ProtobufShowItemCraftingResultPlugIn : IShowItemCraftingResultPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowItemCraftingResultPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ShowResultAsync(CraftingResult result, Item? createdItem)
    {
        var text = result switch
        {
            CraftingResult.Success => createdItem?.Definition is { } definition ? $"Mix succeeded: {definition.Name}" : "Mix succeeded!",
            CraftingResult.Failed => "Mix failed.",
            CraftingResult.NotEnoughMoney => "Not enough zen for this mix.",
            CraftingResult.TooManyItems => "Too many items in the chaos machine.",
            CraftingResult.CharacterLevelTooLow => "Your level is too low for this mix.",
            CraftingResult.LackingMixItems => "Items are missing for this mix.",
            CraftingResult.InvalidItemLevel => "An item has the wrong level for this mix.",
            CraftingResult.CharacterClassTooLow => "Your class can't do this mix.",
            _ => "These items can't be mixed.",
        };

        return _player.SendEnvelopeAsync(new ServerEnvelope { SystemMessage = new S2CSystemMessage { Text = text } });
    }
}
