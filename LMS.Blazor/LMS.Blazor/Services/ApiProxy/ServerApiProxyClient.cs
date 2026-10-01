using LMS.Blazor.Client.Services.ApiProxy;
using LMS.Blazor.Services.Authentication.Tokens;
using LMS.Blazor.Services.Authentication.Tokens.Helpers;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LMS.Blazor.Services.ApiProxy;

public sealed class ServerApiProxyClient(
    HttpClient httpClient,
    AuthenticationStateProvider authenticationStateProvider,
    TokenRefreshService tokenRefreshService) : IApiProxyClient
{
    public async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        var remoteApiUri = $"api/{ApiProxyPath.Validate(endpoint)}";
        using var request = new HttpRequestMessage(method, remoteApiUri)
        {
            Content = content
        };

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var tokenResult = await tokenRefreshService.GetValidAccessTokenAsync(
            authenticationState.User,
            cancellationToken);

        if (tokenResult.Status == AccessTokenStatus.Unauthorized)
            throw new UnauthorizedAccessException("The Blazor session is no longer valid.");

        if (tokenResult.Status == AccessTokenStatus.Unavailable)
            throw new HttpRequestException("The remote authentication service is unavailable.");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenResult.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("The Blazor session is no longer valid.");

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == HttpStatusCode.NoContent ||
            response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}
