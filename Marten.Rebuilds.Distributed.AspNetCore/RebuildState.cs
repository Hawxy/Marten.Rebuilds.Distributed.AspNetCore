using System.Text.Json.Serialization;
namespace Marten.Rebuilds.MultiNode.AspNetCore;

internal static class RebuildState
{
    public const string Unknown = nameof(Unknown);
    public const string Running = nameof(Running);
    public const string Errored = nameof(Errored);
    public const string Completed = nameof(Completed);
}

[JsonDerivedType(typeof(RebuildUnknown), nameof(RebuildUnknown))]
[JsonDerivedType(typeof(RebuildRunning), nameof(RebuildRunning))]
[JsonDerivedType(typeof(RebuildErrored), nameof(RebuildErrored))]
[JsonDerivedType(typeof(RebuildCompleted), nameof(RebuildCompleted))]
public abstract record RebuildStatus(string RebuildState);
public sealed record RebuildUnknown() : RebuildStatus(AspNetCore.RebuildState.Unknown);
public sealed record RebuildRunning(string Projection): RebuildStatus(AspNetCore.RebuildState.Running);
public sealed record RebuildErrored(string Projection, DateTimeOffset RebuiltAt, string ExceptionType) : RebuildStatus(AspNetCore.RebuildState.Errored);
public sealed record RebuildCompleted(HashSet<string> Projections, DateTimeOffset RebuiltAt, string TimeTaken) : RebuildStatus(AspNetCore.RebuildState.Completed);

