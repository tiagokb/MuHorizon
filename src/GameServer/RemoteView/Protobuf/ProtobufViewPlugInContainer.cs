// <copyright file="ProtobufViewPlugInContainer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Protobuf;

using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Auth;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.Characters;
using MUnique.OpenMU.GameServer.RemoteView.Protobuf.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A simple <see cref="ICustomPlugInContainer{IViewPlugIn}"/> that wires the Protobuf view plugins
/// directly to the player, bypassing the legacy client-version-based selection mechanism.
/// </summary>
internal sealed class ProtobufViewPlugInContainer : ICustomPlugInContainer<IViewPlugIn>
{
    private readonly Dictionary<Type, IViewPlugIn> _plugins;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufViewPlugInContainer"/> class.
    /// </summary>
    /// <param name="player">The Protobuf remote player that owns this container.</param>
    public ProtobufViewPlugInContainer(ProtobufRemotePlayer player)
    {
        var all = new IViewPlugIn[]
        {
            new ProtobufShowLoginResultPlugIn(player),
            new ProtobufShowCharacterListPlugIn(player),
            new ProtobufShowCreatedCharacterPlugIn(player),
            new ProtobufShowCharacterCreationFailedPlugIn(player),
            new ProtobufCharacterFocusedPlugIn(player),
            new ProtobufShowCharacterDeleteResponsePlugIn(player),
            new ProtobufEnteredWorldPlugIn(player),
            new ProtobufNewPlayersInScopePlugIn(player),
            new ProtobufNewNpcsInScopePlugIn(player),
            new ProtobufObjectsOutOfScopePlugIn(player),
            new ProtobufSnapshotPlugIn(player),
        };

        _plugins = new Dictionary<Type, IViewPlugIn>();
        foreach (var plugin in all)
        {
            foreach (var iface in plugin.GetType().GetInterfaces())
            {
                if (iface != typeof(IViewPlugIn) && typeof(IViewPlugIn).IsAssignableFrom(iface))
                {
                    _plugins[iface] = plugin;
                }
            }
        }
    }

    /// <inheritdoc />
    public T? GetPlugIn<T>()
        where T : class, IViewPlugIn
        => _plugins.TryGetValue(typeof(T), out var p) ? (T)p : null;
}
