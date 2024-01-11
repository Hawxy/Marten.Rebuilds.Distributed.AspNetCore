using Marten.Rebuilds.MultiNode.AspNetCore.Internal;
using PolyJson;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

// The inheritance sucks but this exists due to some quirks in our existing system, replace with whatever contract shape suits you.
// Using PolyJson to get around the silly type ordering issue with STJ polymorphism (to be fixed in .NET 9)
internal static class RebuildState
{
    public const string Unknown = nameof(Unknown);
    public const string Running = nameof(Running);
    public const string Errored = nameof(Errored);
    public const string Completed = nameof(Completed);
}

[PolyJsonConverter(PolymorphicDefaults.DiscriminatorPropertyName)]
[PolyJsonConverter.SubType(typeof(RebuildUnknown), nameof(RebuildUnknown))]
[PolyJsonConverter.SubType(typeof(RebuildRunning), nameof(RebuildRunning))]
[PolyJsonConverter.SubType(typeof(RebuildErrored), nameof(RebuildErrored))]
[PolyJsonConverter.SubType(typeof(RebuildCompleted), nameof(RebuildCompleted))]
public abstract record RebuildStatus(string RebuildState) : JsonInheritanceBase;
public sealed record RebuildUnknown() : RebuildStatus(AspNetCore.RebuildState.Unknown);
public sealed record RebuildRunning(string Projection): RebuildStatus(AspNetCore.RebuildState.Running);
public sealed record RebuildErrored(string Projection, DateTimeOffset RebuiltAt, string ExceptionType) : RebuildStatus(AspNetCore.RebuildState.Errored);
public sealed record RebuildCompleted(HashSet<string> Projections, DateTimeOffset RebuiltAt, string TimeTaken) : RebuildStatus(AspNetCore.RebuildState.Completed);

