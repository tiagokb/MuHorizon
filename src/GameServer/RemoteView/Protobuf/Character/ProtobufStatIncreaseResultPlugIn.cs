// <copyright file="ProtobufStatIncreaseResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Views.Character;

internal sealed class ProtobufStatIncreaseResultPlugIn : IStatIncreaseResultPlugIn
{
    private readonly ProtobufUpdateCharacterStatePlugIn _statsSender;

    public ProtobufStatIncreaseResultPlugIn(ProtobufRemotePlayer player)
        => _statsSender = new ProtobufUpdateCharacterStatePlugIn(player);

    public ValueTask StatIncreaseResultAsync(AttributeDefinition statType, ushort addedPoints)
        => _statsSender.UpdateCharacterStatsAsync();
}
