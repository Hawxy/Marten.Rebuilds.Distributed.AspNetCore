using System.Diagnostics;
using Marten.Events.Daemon;
using Marten.Events.Daemon.Resiliency;
using Marten.Rebuilds.MultiNode.AspNetCore.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public sealed class RebuildService(
    IDocumentStore documentStore,
    IRebuildCache cache,
    ILogger<RebuildService> logger,
    IBus bus,
    ILocalDaemon localDaemon)
    : IRebuildService
{
    public async Task RequestRebuild(HashSet<string> projections)
    {
        if (await IsRebuilding())
            return;
        
        if (documentStore.Options.Events.Daemon.AsyncMode is DaemonMode.Disabled or DaemonMode.Solo)
        {
            // If the daemon isn't running or we know it's the current node we'll just rebuild immediately and skip the rest (used for local development)
            logger.LogInformation("Single node environment detected, short-circuiting rebuild request to current node");
            await RunRebuild(projections);
            return;
        }

        await bus.Publish(new RebuildRequested(projections));
    }

    public ValueTask<bool> IsRebuilding() => cache.IsRebuilding();
    
    public async Task RunRebuild(HashSet<string> projections)
    {
        if (!localDaemon.IsCurrentNodeDaemon())
            return;

        await cache.SetRebuildPending();
        await localDaemon.StopAsync();
        
        var projectionsFormatted = string.Join(',', projections);
        logger.LogInformation("Rebuild requested, backend is now in read-only mode. Projections: {projections}", projectionsFormatted);
        var stopwatch = Stopwatch.StartNew();
        
        using var daemon = await documentStore.BuildProjectionDaemonAsync(logger: logger);

        var listener = new RebuildExceptionListener();
        daemon.Tracker.Subscribe(listener);

        try
        {
            foreach (var projection in projections)
            {
                await cache.SetRebuildRunning(projection);
                // Caution: shard timeout needs to be increased if single projection rebuilds exceed 10 minutes.
                await daemon.RebuildProjectionAsync(projection, TimeSpan.FromMinutes(10), CancellationToken.None);
            }

            stopwatch.Stop();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error occurred whilst performing rebuild");
            await cache.SetErrored(ex.GetType().Name);
        }
        finally
        {
            var innerEx = listener.GetException();
            if (innerEx is not null)
            {
                await cache.SetErrored(innerEx.GetType().Name);
                logger.LogError(innerEx, "Rebuilding finished in {time} with inner exception. Projections might have not been rebuilt correctly: {projections}", stopwatch.Elapsed.ToString("g"), projectionsFormatted);
            }
            else
            {
                await cache.SetRebuildFinished(projections, stopwatch.Elapsed.ToString("g"));
                logger.LogInformation("Rebuilding finished in {time}. Projections rebuilt: {projections}", stopwatch.Elapsed.ToString("g"), projectionsFormatted);
            }
            
            await daemon.StopAllAsync();
            await localDaemon.StartAsync();
        }
    }
}


public sealed class RebuildExceptionListener : IObserver<ShardState>
{
    private Exception? _exception;
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public Exception? GetException()
    {
        return _exception;
    }

    public void OnNext(ShardState value)
    {
        if (value.Exception is not null)
        {
            _exception = value.Exception;
        }
    }
}

public interface IRebuildService
{
    /// <summary>
    /// Publishes a message to request a rebuild.
    /// </summary>
    /// <param name="projections"></param>
    /// <returns></returns>
    Task RequestRebuild(HashSet<string> projections);

    /// <summary>
    /// Checks if the rebuild is currently running
    /// </summary>
    /// <returns></returns>
    ValueTask<bool> IsRebuilding();
    
    /// <summary>
    /// Runs the rebuild with the supplied set of projections.
    /// </summary>
    /// <param name="projections"></param>
    /// <returns></returns>
    Task RunRebuild(HashSet<string> projections);
}