using ZiggyCreatures.Caching.Fusion;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public sealed class RebuildCache(IFusionCache cache) : IRebuildCache
{
    private const string RebuildCacheKey = nameof(RebuildCache);

    public async ValueTask SetRebuildPending()
    {
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, new RebuildRunning("**PENDING**"), options =>
        {
            options.Duration = TimeSpan.FromSeconds(10);
        });
    }

    public async ValueTask SetRebuildRunning(string projection)
    {
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, new RebuildRunning(projection), options =>
        {
            // We want the system to default back to a good state within 10 minutes if something goes horrifically wrong for a given projection
            options.Duration = TimeSpan.FromMinutes(10);
        });
    }

    public async ValueTask SetErrored(string exceptionType)
    {
        var currentStatus = await cache.GetOrDefaultAsync<RebuildStatus>(RebuildCacheKey);

        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, new RebuildErrored(currentStatus is RebuildRunning running ? running.Projection : string.Empty, DateTimeOffset.UtcNow, exceptionType), options =>
        {
            // Remove rebuild status after an hour
            options.Duration = TimeSpan.FromMinutes(60);
        });
    }
    
    public async ValueTask SetRebuildFinished(HashSet<string> projections, string timeTaken)
    {
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, new RebuildCompleted(projections, DateTimeOffset.UtcNow, timeTaken), options =>
        {
            // Remove rebuild status after an hour
            options.Duration = TimeSpan.FromMinutes(60);
        });
    }

    public async ValueTask<bool> IsRebuilding()
    {
        var state = await cache.GetOrDefaultAsync<RebuildStatus>(RebuildCacheKey);
        return state is RebuildRunning;
    }

    public async ValueTask<RebuildStatus> GetCurrentState()
    {
        return (await cache.GetOrDefaultAsync<RebuildStatus>(RebuildCacheKey, new RebuildUnknown()))!;
    }
}

public interface IRebuildCache
{
    ValueTask SetRebuildPending();
    ValueTask SetRebuildRunning(string projection);

    ValueTask SetErrored(string description);

    ValueTask SetRebuildFinished(HashSet<string> projections, string timeTaken);

    ValueTask<bool> IsRebuilding();

    ValueTask<RebuildStatus> GetCurrentState();
}