// <copyright file="ProtobufCloseVaultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Vault;

using MUnique.OpenMU.GameLogic.Views.Vault;

// Client initiates vault close via C2SCloseVault; server-initiated close is a no-op for the Protobuf client.
internal sealed class ProtobufCloseVaultPlugIn : ICloseVaultPlugIn
{
    public ProtobufCloseVaultPlugIn(ProtobufRemotePlayer player) { }

    public ValueTask CloseVaultAsync() => ValueTask.CompletedTask;
}
