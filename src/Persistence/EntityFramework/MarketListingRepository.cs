// <copyright file="MarketListingRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Loads market listings as whole aggregates (the custody storage with its item, options and all),
/// like accounts are loaded: the generic loading would look the items up in the merchant store
/// items of the configuration and come back empty.
/// </summary>
internal class MarketListingRepository : CachingGenericRepository<MarketListing>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketListingRepository"/> class.
    /// </summary>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public MarketListingRepository(IContextAwareRepositoryProvider repositoryProvider, ILoggerFactory loggerFactory)
        : base(repositoryProvider, loggerFactory)
    {
    }

    /// <inheritdoc />
    public override async ValueTask<MarketListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        (this.RepositoryProvider as ICacheAwareRepositoryProvider)?.EnsureCachesForCurrentGameConfiguration();

        using var context = this.GetContext();
        await context.Context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var listing = await new AccountJsonObjectLoader().LoadObjectAsync<MarketListing>(id, context.Context, cancellationToken).ConfigureAwait(false);
            if (listing is not null)
            {
                context.Context.Attach(listing);
            }

            return listing;
        }
        finally
        {
            await context.Context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async ValueTask<IEnumerable<MarketListing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        (this.RepositoryProvider as ICacheAwareRepositoryProvider)?.EnsureCachesForCurrentGameConfiguration();

        using var context = this.GetContext();
        var ids = await context.Context.Set<MarketListing>().AsNoTracking().Select(l => l.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<MarketListing>(ids.Count);
        await context.Context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var objectLoader = new AccountJsonObjectLoader();
            foreach (var id in ids)
            {
                if (await objectLoader.LoadObjectAsync<MarketListing>(id, context.Context, cancellationToken).ConfigureAwait(false) is { } listing)
                {
                    context.Context.Attach(listing);
                    result.Add(listing);
                }
            }
        }
        finally
        {
            await context.Context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return result;
    }
}
