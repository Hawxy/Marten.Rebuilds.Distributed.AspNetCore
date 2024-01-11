using System.Reflection;
using Marten.Events.Daemon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Timer = System.Timers.Timer;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public interface ILocalDaemon
{
    Task StopAsync();
    Task StartAsync();
    bool IsCurrentNodeDaemon();
}

public sealed class LocalDaemon(IDocumentStore store, IEnumerable<IHostedService> hostedServices) : ILocalDaemon
{
    private readonly AsyncProjectionHostedService _service = hostedServices
        .OfType<AsyncProjectionHostedService>()
        .Single();

    // we want to start and stop the daemon directly so we keep the rebuild node's advisory lock within the coordinator.
    private IProjectionDaemon Daemon => _service.Coordinators.Single().Daemon;
    
    public Task StartAsync() => Daemon.StartAllShards();
    
    private static FieldInfo? _hotColdTimerQuery;
    private static bool IsHotColdCoordinatorWaiting(INodeCoordinator coordinator)
    {
        if (_hotColdTimerQuery == null)
        {
            var methodName = "_timer";
            var type = typeof(AsyncProjectionHostedService).Assembly.GetType("Marten.Events.Daemon.HotColdCoordinator")!;
            _hotColdTimerQuery = type.GetField(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        }

        var timer = _hotColdTimerQuery.GetValue(coordinator) as Timer;
        return timer is not null;
    }
    
    public Task StopAsync() => Daemon.StopAll();

    public bool IsCurrentNodeDaemon()
    {
        // If tracker is null or HWM reports as zero then this node does not have a running daemon.
        if (store.Storage.Database.Tracker is null || store.Storage.Database.Tracker.HighWaterMark == 0)
            return false;
        
        // To be absolutely sure, we check if the coordinator's timer is null. If it is, this node has the lock. (a better API here would be nice).
        return !IsHotColdCoordinatorWaiting(_service.Coordinators.Single());
    }

}