// <copyright file="Listener.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;
using Pipelines.Sockets.Unofficial;

/// <summary>
/// A minimal TCP listener that accepts connections and wraps them as <see cref="IConnection"/>.
/// Crypto parameters from the legacy stack are accepted but ignored.
/// </summary>
public class Listener
{
    private readonly int _port;
    private readonly ILoggerFactory _loggerFactory;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private bool _stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class.
    /// </summary>
    /// <param name="port">The TCP port to listen on.</param>
    /// <param name="decryptorFactory">Ignored — kept for API compatibility.</param>
    /// <param name="encryptorFactory">Ignored — kept for API compatibility.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public Listener(int port, object? decryptorFactory, object? encryptorFactory, ILoggerFactory loggerFactory)
    {
        _port = port;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Raised before a new socket is accepted, allowing callers to cancel (e.g. IP-ban check).
    /// </summary>
    public event AsyncEventHandler<ClientAcceptingEventArgs>? ClientAccepting;

    /// <summary>
    /// Raised when a new <see cref="IConnection"/> has been created for an accepted socket.
    /// </summary>
    public event AsyncEventHandler<ClientAcceptedEventArgs>? ClientAccepted;

    /// <summary>
    /// Starts accepting connections.
    /// </summary>
    /// <param name="backlog">The listen backlog.</param>
    public void Start(int backlog)
    {
        _cts = new CancellationTokenSource();
        _tcpListener = new TcpListener(IPAddress.Any, _port);
        _tcpListener.Start(backlog);
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the listener.
    /// </summary>
    public void Stop()
    {
        _stopped = true;
        _cts?.Cancel();
        _tcpListener?.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!_stopped && !ct.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await _tcpListener!.AcceptSocketAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (_stopped)
            {
                break;
            }

            var accepting = new ClientAcceptingEventArgs(socket);
            if (ClientAccepting != null)
            {
                await ClientAccepting(accepting).ConfigureAwait(false);
            }

            if (accepting.Cancel)
            {
                socket.Dispose();
                continue;
            }

            var socketConn = SocketConnection.Create(socket);
            var connection = new Connection(socketConn, _loggerFactory.CreateLogger<Connection>());

            if (ClientAccepted != null)
            {
                await ClientAccepted(new ClientAcceptedEventArgs(connection)).ConfigureAwait(false);
            }
        }
    }
}