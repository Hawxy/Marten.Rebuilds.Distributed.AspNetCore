using Marten.Events.Aggregation;

namespace Marten.Rebuilds.MultiNode.AspNetCore.Api;

public sealed record WeatherReported(Guid Id, DateTime DateTime, int TemperatureC, string Summary);
    
public sealed class Weather
{
    public Guid Id { get; set; }
    public DateTime DateTime { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}

public sealed class WeatherProjection : SingleStreamProjection<Weather>
{
    public WeatherProjection()
    {
        CreateEvent<WeatherReported>(reported => new Weather()
        {
            Id = reported.Id,
            DateTime = reported.DateTime,
            TemperatureC = reported.TemperatureC,
            Summary = reported.Summary
        });
    }
}