// <copyright file="IItemSellConfirmRequiredPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Inventory;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Interface for a view plugin that notifies the client that selling a valuable item requires confirmation.
/// </summary>
public interface IItemSellConfirmRequiredPlugIn : IViewPlugIn
{
    /// <summary>Asks the client to confirm selling the specified item at the given price.</summary>
    ValueTask ItemSellConfirmRequiredAsync(Item item, long sellPrice);
}
