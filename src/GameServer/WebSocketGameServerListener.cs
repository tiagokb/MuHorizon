// <copyright file="WebSocketGameServerListener.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer;

using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A game server listener that accepts WebSocket connections on a configurable port.
/// TLS is terminated upstream by nginx; this listener handles plain ws:// only.
/// </summary>
public sealed class WebSocketGameServerListener : IGameServerListener
{
    private readonly int _port;
    private readonly IGameServerContext _gameContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WebSocketGameServerListener> _logger;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketGameServerListener"/> class.
    /// </summary>
    /// <param name="port">The TCP port to listen on.</param>
    /// <param name="gameContext">The game server context used to create players.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public WebSocketGameServerListener(int port, IGameServerContext gameContext, ILoggerFactory loggerFactory)
    {
        _port = port;
        _gameContext = gameContext;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WebSocketGameServerListener>();
    }

    /// <inheritdoc/>
    public event AsyncEventHandler<PlayerConnectedEventArgs>? PlayerConnected;

    /// <inheritdoc/>
    public async ValueTask StartAsync()
    {
        _cts = new CancellationTokenSource();
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://+:{_port}/ws/");
        _httpListener.Start();
        _logger.LogInformation("WebSocket listener started on port {Port}", _port);

        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _cts?.Cancel();
        if (_httpListener is { IsListening: true })
        {
            _httpListener.Stop();
        }

        _logger.LogInformation("WebSocket listener stopped");
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? context;
            try
            {
                context = await _httpListener!.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting HTTP context on port {Port}", _port);
                continue;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            _ = Task.Run(() => HandleWebSocketAsync(context, ct), ct);
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
    {
        HttpListenerWebSocketContext? wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket handshake failed for {RemoteEndPoint}", context.Request.RemoteEndPoint);
            context.Response.Close();
            return;
        }

        var remoteEndPoint = context.Request.RemoteEndPoint;
        _logger.LogInformation("WebSocket client connected from {RemoteEndPoint}", remoteEndPoint);

        var connection = new WebSocketConnection(
            wsContext.WebSocket,
            remoteEndPoint,
            _loggerFactory.CreateLogger<WebSocketConnection>());

        var player = new ProtobufRemotePlayer(_gameContext, connection);

        if (PlayerConnected is { } handler)
        {
            await handler(new PlayerConnectedEventArgs(player)).ConfigureAwait(false);
        }

        _ = Task.Run(connection.BeginReceiveAsync, ct);
    }
}
