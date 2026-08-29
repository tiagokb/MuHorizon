// <copyright file="ProtobufUpdateMoneyPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;

using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufUpdateMoneyPlugIn : IUpdateMoneyPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufUpdateMoneyPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask UpdateMoneyAsync() =>
        _player.SendEnvelopeAsync(new ServerEnvelope { MoneyUpdate = new S2CMoneyUpdate { Money = (uint)_player.Money } });
}
