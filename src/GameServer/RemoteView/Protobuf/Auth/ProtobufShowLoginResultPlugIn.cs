// <copyright file="ProtobufShowLoginResultPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf.Auth;

using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Network.Protobuf;

/// <summary>
/// Sends <see cref="S2CLoginResult"/> to the client via the Protobuf/WebSocket protocol.
/// </summary>
public sealed class ProtobufShowLoginResultPlugIn : IShowLoginResultPlugIn
{
    private readonly ProtobufRemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufShowLoginResultPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ProtobufShowLoginResultPlugIn(ProtobufRemotePlayer player) => _player = player;

    /// <inheritdoc />
    public ValueTask ShowLoginResultAsync(LoginResult loginResult)
        => _player.SendEnvelopeAsync(new ServerEnvelope
        {
            LoginResult = new S2CLoginResult { Code = MapCode(loginResult) },
        });

    private static LoginResultCode MapCode(LoginResult r) => r switch
    {
        LoginResult.Ok => LoginResultCode.Ok,
        LoginResult.InvalidPassword => LoginResultCode.InvalidPassword,
        LoginResult.AccountBlocked => LoginResultCode.AccountBlocked,
        LoginResult.AccountAlreadyConnected => LoginResultCode.AccountAlreadyConnected,
        LoginResult.TemporaryBlocked => LoginResultCode.TemporaryBlocked,
        _ => LoginResultCode.ConnectionError,
    };
}
