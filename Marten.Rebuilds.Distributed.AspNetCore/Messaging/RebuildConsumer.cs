using MassTransit;

namespace Marten.Rebuilds.MultiNode.AspNetCore.Messaging;

public record RebuildRequested(HashSet<string> Projections);

public sealed class RebuildConsumer(IRebuildService rebuildService) : IConsumer<RebuildRequested>
{
    public async Task Consume(ConsumeContext<RebuildRequested> context)
    {
        await rebuildService.RunRebuild(context.Message.Projections);
    }
}