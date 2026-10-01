using LMS.Blazor.Client.Services.ApiProxy;
using LMS.Blazor.Services.ApiProxy;
using LMS.Blazor.Services.Authentication.Tokens;
using LMS.Blazor.Services.Authentication.Tokens.Helpers;
using Microsoft.AspNetCore.Antiforgery;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace LMS.Blazor.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddRemoteApiProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Client components are created once on the server during prerendering.
        // The browser implementation performs the real same-origin proxy request.
        //services.AddScoped<IApiProxyClient, ServerApiProxyClient>();
        var remoteApiBaseUrl = configuration[
            "ReverseProxy:Clusters:remote-api:Destinations:primary:Address"]
            ?? throw new InvalidOperationException(
                "The Remote API destination is not configured.");

        services.AddHttpClient<IApiProxyClient, ServerApiProxyClient>(httpClient =>
            httpClient.BaseAddress = new Uri(remoteApiBaseUrl));


        // Browser requests authenticate to the BFF with a cookie. Validate an
        // antiforgery token for every unsafe method before YARP adds the JWT and
        // forwards the request. Explicit validation also covers DELETE.
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
            {
                transformBuilderContext.AddRequestTransform(async transformContext =>
                {
                    var httpContext = transformContext.HttpContext;

                    if (RequiresAntiforgery(httpContext.Request.Method) &&
                        !await HasValidAntiforgeryTokenAsync(httpContext))
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status400BadRequest;
                        return;
                    }

                    var tokenRefreshService = httpContext.RequestServices
                        .GetRequiredService<TokenRefreshService>();

                    var tokenResult = await tokenRefreshService.GetValidAccessTokenAsync(
                        httpContext.User,
                        httpContext.RequestAborted);

                    if (tokenResult.Status == AccessTokenStatus.Unauthorized)
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status401Unauthorized;
                        return;
                    }

                    if (tokenResult.Status == AccessTokenStatus.Unavailable)
                    {
                        httpContext.Response.StatusCode =
                            StatusCodes.Status503ServiceUnavailable;
                        return;
                    }

                    transformContext.ProxyRequest.Headers.Authorization =
                        new AuthenticationHeaderValue(
                            "Bearer",
                            tokenResult.AccessToken);
                });
            });

        return services;
    }

    public static IEndpointConventionBuilder MapRemoteApiProxy(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapReverseProxy()
            .RequireAuthorization();

    private static async Task<bool> HasValidAntiforgeryTokenAsync(
        HttpContext httpContext)
    {
        var antiforgery = httpContext.RequestServices
            .GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool RequiresAntiforgery(string method) =>
        !HttpMethods.IsGet(method) &&
        !HttpMethods.IsHead(method) &&
        !HttpMethods.IsOptions(method) &&
        !HttpMethods.IsTrace(method);
}
