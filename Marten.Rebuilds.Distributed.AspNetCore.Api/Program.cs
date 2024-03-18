using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Marten.Rebuilds.MultiNode.AspNetCore;
using Marten.Rebuilds.MultiNode.AspNetCore.Api;
using Marten.Services.Json;
using Microsoft.AspNetCore.Mvc;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

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
    
    _.UseDefaultSerialization(EnumStorage.AsString, serializerType: SerializerType.SystemTextJson);

}).AddAsyncDaemon(DaemonMode.HotCold)
    .UseLightweightSessions()
    .UseNpgsqlDataSource()
    .ApplyAllDatabaseChangesOnStartup()
    .OptimizeArtifactWorkflow();

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

app.UseCors();

// middleware should go after authentication/authorization
app.UseRebuildMiddleware();

var rebuild = app.MapGroup("rebuild");

// get a list of rebuilds to show on the front end
rebuild.MapGet("projections",
    (IDocumentStore store) => store.Options.Events.Projections().Select(x => x.ProjectionName).ToArray());

// kick off the rebuild for a given projection
rebuild.MapPost("run",
    async ([FromServices] IRebuildService rebuildService, [FromBody] HashSet<string> projections) =>
    await rebuildService.RequestRebuild(projections));

// display the current rebuild state to the user
rebuild.MapGet("status", async ([FromServices] IRebuildCache cache) => await cache.GetCurrentState());

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
