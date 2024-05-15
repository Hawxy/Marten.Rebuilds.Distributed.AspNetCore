using Marten.Events.Daemon.Coordination;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public interface ILocalDaemon
{
    Task StopAsync();
    Task StartAsync();
    bool IsCurrentNodeDaemon();
}

public sealed class LocalDaemon(IProjectionCoordinator coordinator) : ILocalDaemon
{
    public async Task StartAsync() => await coordinator.ResumeAsync();

    public async Task StopAsync() => await coordinator.PauseAsync();

    public bool IsCurrentNodeDaemon()
    {
        return coordinator.DaemonForMainDatabase().IsRunning;
    }
}