// <copyright file="ProtobufObjectsOutOfScopePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufObjectsOutOfScopePlugIn : IObjectsOutOfScopePlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufObjectsOutOfScopePlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask ObjectsOutOfScopeAsync(IEnumerable<IIdentifiable> objects)
    {
        var msg = new S2CEntitiesOutOfScope();
        foreach (var obj in objects)
        {
            msg.EntityIds.Add(obj.Id);
        }

        if (msg.EntityIds.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        return _player.SendEnvelopeAsync(new ServerEnvelope { EntitiesOutOfScope = msg });
    }
}
