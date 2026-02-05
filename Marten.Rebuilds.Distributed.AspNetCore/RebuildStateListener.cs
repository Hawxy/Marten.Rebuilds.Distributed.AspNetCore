using System.Threading.Channels;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public sealed class RebuildStateListener : IDisposable
{
    private readonly IRebuildCache _cache;
    private readonly Channel<RebuildStatus> _rebuildStatusChannel = Channel.CreateBounded<RebuildStatus>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest});

    public RebuildStateListener(IRebuildCache cache)
    {
        _cache = cache;
        _cache.StatusUpdated += CacheOnStatusUpdated;
    }

    private void CacheOnStatusUpdated(object? sender, RebuildStatus e)
    {
        _rebuildStatusChannel.Writer.TryWrite(e);
    }

    public async ValueTask<RebuildStatus> GetNextUpdateAsync(CancellationToken ct)
    {
        return await _rebuildStatusChannel.Reader.ReadAsync(ct);
    }
    
    public void Dispose()
    {
        _rebuildStatusChannel.Writer.TryComplete();
        _cache.StatusUpdated -= CacheOnStatusUpdated;
    }
}