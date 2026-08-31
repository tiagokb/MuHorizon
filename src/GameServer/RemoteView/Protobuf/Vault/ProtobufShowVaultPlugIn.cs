// <copyright file="ProtobufShowVaultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Vault;

using System.Linq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.Vault;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Inventory;
using MUnique.OpenMU.Network.Protobuf;

internal sealed class ProtobufShowVaultPlugIn : IShowVaultPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufShowVaultPlugIn(ProtobufRemotePlayer player) => _player = player;

    public async ValueTask ShowVaultAsync()
    {
        var vault   = _player.Vault;
        var account = _player.Account;
        if (vault is null || account is null)
            return;

        var msg = new S2CVaultOpened
        {
            Money       = (uint)(account.Vault?.Money ?? 0),
            IsExtended  = account.IsVaultExtended,
        };

        foreach (var item in vault.Items.Where(i => i.Definition is not null))
            msg.Items.Add(ProtobufUpdateInventoryListPlugIn.BuildItemData(item));

        await _player.SendEnvelopeAsync(new ServerEnvelope { VaultOpened = msg }).ConfigureAwait(false);
    }
}
