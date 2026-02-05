using ZiggyCreatures.Caching.Fusion;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public sealed class RebuildCache(IFusionCache cache) : IRebuildCache
{
    private const string RebuildCacheKey = nameof(RebuildCache);
    public event EventHandler<RebuildStatus>? StatusUpdated;
    
    public async ValueTask SetRebuildPending()
    {
        var state = new RebuildRunning("**PENDING**");
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, state, options =>
        {
            options.Duration = TimeSpan.FromSeconds(10);
        });
        
        StatusUpdated?.Invoke(this, state);
    }

    public async ValueTask SetRebuildRunning(string projection)
    {
        var state = new RebuildRunning(projection);
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, state, options =>
        {
            // We want the system to default back to a good state within 10 minutes if something goes horrifically wrong for a given projection
            options.Duration = TimeSpan.FromMinutes(10);
        });

        StatusUpdated?.Invoke(this, state);
    }

    public async ValueTask SetErrored(string exceptionType)
    {
        var currentStatus = await cache.GetOrDefaultAsync<RebuildStatus>(RebuildCacheKey);

        var errorState = new RebuildErrored(currentStatus is RebuildRunning running ? running.Projection : string.Empty,
            DateTimeOffset.UtcNow, exceptionType);

        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, errorState, options =>
        {
            // Remove rebuild status after an hour
            options.Duration = TimeSpan.FromMinutes(60);
        });
        
        StatusUpdated?.Invoke(this, errorState);
    }
    
    public async ValueTask SetRebuildFinished(HashSet<string> projections, string timeTaken)
    {
        var state = new RebuildCompleted(projections, DateTimeOffset.UtcNow, timeTaken);
        await cache.SetAsync<RebuildStatus>(RebuildCacheKey, state, options =>
        {
            // Remove rebuild status after an hour
            options.Duration = TimeSpan.FromMinutes(60);
        });
        
        StatusUpdated?.Invoke(this, state);
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
    public event EventHandler<RebuildStatus>? StatusUpdated;
    ValueTask SetRebuildPending();
    ValueTask SetRebuildRunning(string projection);

    ValueTask SetErrored(string description);

    ValueTask SetRebuildFinished(HashSet<string> projections, string timeTaken);

    ValueTask<bool> IsRebuilding();

    ValueTask<RebuildStatus> GetCurrentState();
}