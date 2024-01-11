using System.ComponentModel;
using System.Text.Json.Serialization;
using PolyJson;

namespace Marten.Rebuilds.MultiNode.AspNetCore.Internal;

public static class PolymorphicDefaults
{
    public const string DiscriminatorPropertyName = "$type";
}

public abstract record JsonInheritanceBase
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [JsonPropertyName(PolymorphicDefaults.DiscriminatorPropertyName)]
    public string Discriminator => DiscriminatorValue.Get(GetType())!;
}