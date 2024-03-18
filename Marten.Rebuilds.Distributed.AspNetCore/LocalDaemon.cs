using System.Reflection;
using Marten.Events.Daemon;
using Marten.Events.Daemon.Coordination;
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

public sealed class LocalDaemon(IProjectionCoordinator coordinator) : ILocalDaemon
{
    public async Task StartAsync()
    {
        await coordinator.DaemonForMainDatabase().StartAllAsync();
    }

    public async Task StopAsync() => await coordinator.DaemonForMainDatabase().StopAllAsync();

    public bool IsCurrentNodeDaemon()
    {
        return coordinator.DaemonForMainDatabase().IsRunning;
    }
}