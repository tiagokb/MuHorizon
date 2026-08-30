// <copyright file="ProtobufOpenNpcWindowPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.NPC;

internal sealed class ProtobufOpenNpcWindowPlugIn : IOpenNpcWindowPlugIn
{
    public ProtobufOpenNpcWindowPlugIn(ProtobufRemotePlayer player) { }

    public ValueTask OpenNpcWindowAsync(NpcWindow window) => ValueTask.CompletedTask;
}
