using Marten.Rebuilds.MultiNode.AspNetCore.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public static class ServiceCollectionExtensions
{
    public static void AddFusionCache(this WebApplicationBuilder builder)
    {
        // exclude Redis when environment != production, only included for demo purposes
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
        }); 
        
        builder.Services.AddFusionCacheStackExchangeRedisBackplane(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
        });
        
        builder.Services.AddFusionCacheSystemTextJsonSerializer();
        builder.Services.AddFusionCache().TryWithAutoSetup();
    }

    public static void AddMassTransitMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<RebuildConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));

                // each node requires its own endpoint as all endpoints need to receive the message.
                cfg.ReceiveEndpoint(new TemporaryEndpointDefinition("rebuild", 1),e=>
                {
                    e.Exclusive = true;
                    e.ConfigureConsumer<RebuildConsumer>(context);
                });
                
                cfg.ConfigureEndpoints(context);
            });
        });
    }
    
    // this MUST be called after marten's own registration.
    public static void AddRebuildMiddleware(this IServiceCollection services)
    {
        services.AddSingleton<IRebuildService, RebuildService>();
        services.AddSingleton<IRebuildCache, RebuildCache>();
        services.AddSingleton<ILocalDaemon, LocalDaemon>();
    }
}