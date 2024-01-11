using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Marten.Rebuilds.MultiNode.AspNetCore;

public sealed class RebuildMiddleware(RequestDelegate next)
{
    private readonly HashSet<string> _rebuildBlockedMethods = [HttpMethod.Post.Method, HttpMethod.Delete.Method, HttpMethod.Patch.Method, HttpMethod.Put.Method];

    private const string RebuildRoute = "/rebuild/run";
    public async Task InvokeAsync(HttpContext context, IRebuildService rebuildService)
    {
        if (context.Request.Path != RebuildRoute && _rebuildBlockedMethods.Contains(context.Request.Method) && await rebuildService.IsRebuilding())
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Service is currently in read-only mode", context.RequestAborted);
            return;
        }

        await next(context);
    }
}

public static class ApplicationBuilderExtensions
{
    public static void UseRebuildMiddleware(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<RebuildMiddleware>();
    }
}