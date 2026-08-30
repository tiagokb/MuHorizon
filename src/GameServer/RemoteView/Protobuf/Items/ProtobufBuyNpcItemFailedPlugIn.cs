// <copyright file="ProtobufBuyNpcItemFailedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Items;

using MUnique.OpenMU.GameLogic.Views.Inventory;

internal sealed class ProtobufBuyNpcItemFailedPlugIn : IBuyNpcItemFailedPlugIn
{
    public ProtobufBuyNpcItemFailedPlugIn(ProtobufRemotePlayer player) { }

    public ValueTask BuyNpcItemFailedAsync() => ValueTask.CompletedTask;
}
