// <copyright file="ProtobufLogoutPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Character;

using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Confirms a logout (C2SLogout) with <see cref="S2CLogout"/>: the client then shows the character
/// selection, or the login screen (the server disconnects right after for <see cref="LogoutType.CloseGame"/>).
/// </summary>
internal sealed class ProtobufLogoutPlugIn : ILogoutPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    public ProtobufLogoutPlugIn(ProtobufRemotePlayer player) => _player = player;

    public ValueTask LogoutAsync(LogoutType logoutType)
    {
        return _player.SendEnvelopeAsync(new ServerEnvelope
        {
            Logout = new S2CLogout
            {
                Kind = logoutType == LogoutType.BackToCharacterSelection ? LogoutKind.CharacterSelect : LogoutKind.LoginScreen,
            },
        });
    }
}
