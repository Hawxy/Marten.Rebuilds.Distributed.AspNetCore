using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Rebuilds.MultiNode.AspNetCore;
using Marten.Rebuilds.MultiNode.AspNetCore.Api;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
    .MinimumLevel.Override("Npgsql.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Async(a => a.Console()));

// Add services to the container.

builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!);

builder.Services.AddMarten(_ =>
    {
        _.CreateDatabasesForTenants(c =>
        {
            c.ForTenant()
                .CheckAgainstPgDatabase()
                .WithEncoding("UTF-8")
                .ConnectionLimit(-1);
        });

        _.Projections.Add<WeatherProjection>(ProjectionLifecycle.Async);

        _.UseSystemTextJsonForSerialization(EnumStorage.AsString);

    }).AddAsyncDaemon(DaemonMode.HotCold)
    .UseLightweightSessions()
    .UseNpgsqlDataSource()
    .ApplyAllDatabaseChangesOnStartup();

builder.Services.AddRebuildMiddleware();
builder.AddFusionCache();
builder.AddMassTransitMessaging();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(x =>
    {
        x.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
      
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSerilogRequestLogging();
app.UseCors();

// middleware should go after authentication/authorization
app.UseRebuildMiddleware();

var rebuild = app.MapGroup("rebuild");

// get a list of rebuilds to show on the front end
rebuild.MapGet("projections",
    (IDocumentStore store) => store.Options.Events.Projections().Select(x => x.Name).ToArray());

// kick off the rebuild for a given projection
rebuild.MapPost("run",
    async ([FromServices] IRebuildService rebuildService, [FromBody] HashSet<string> projections) =>
    await rebuildService.RequestRebuild(projections));

rebuild.MapGet("status", ([FromServices] IRebuildCache cache, CancellationToken cancellationToken) =>
{
    async IAsyncEnumerable<RebuildStatus> StreamStatus()
    {
        yield return await cache.GetCurrentState();
        
        using var listener = new RebuildStateListener(cache);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await listener.GetNextUpdateAsync(cancellationToken);
        }
    }

    return TypedResults.ServerSentEvents(StreamStatus());
});

// seed some example events
app.MapPost("seed", async (IDocumentSession session) =>
{
    for (var i = 0; i < 100_000; i++)
    {
        var id = Guid.NewGuid();
        session.Events.Append(id,
            new WeatherReported(id, DateTime.UtcNow, Random.Shared.Next(-50, 60), "Foo Bar Random"));
    }

    await session.SaveChangesAsync();
});

app.MapPost("add", async (IDocumentSession session, IDocumentStore store, ILogger<WeatherReported> logger) =>
{
    var id = Guid.NewGuid();
    session.Events.Append(id,
        new WeatherReported(id, DateTime.UtcNow, Random.Shared.Next(-50, 60), "Foo Bar Random"));
    await session.SaveChangesAsync();
});
    
app.Run();
