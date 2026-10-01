using System.Net;
using System.Net.Http.Json;

namespace LMS.Blazor.Client.Services.ApiProxy;

public sealed class ApiProxyClient(HttpClient httpClient) : IApiProxyClient
{
    public async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        var proxyUri = ApiProxyPath.Validate(endpoint);
        using var request = new HttpRequestMessage(method, proxyUri)
        {
            Content = content
        };

        if (RequiresAntiforgery(method))
        {
            var requestToken = await GetAntiforgeryTokenAsync(cancellationToken);
            request.Headers.Add(AntiforgeryProtocol.HeaderName, requestToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        EnsureAuthenticated(response);
        response.EnsureSuccessStatusCode();

        if (response.StatusCode == HttpStatusCode.NoContent ||
            response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            AntiforgeryProtocol.TokenEndpoint,
            cancellationToken);

        EnsureAuthenticated(response);
        response.EnsureSuccessStatusCode();

        var token = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenDto>(cancellationToken);

        return !string.IsNullOrWhiteSpace(token?.RequestToken)
            ? token.RequestToken
            : throw new InvalidOperationException(
                "The BFF returned an invalid antiforgery token response.");
    }

    private static void EnsureAuthenticated(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("The Blazor session is no longer valid.");
    }

    private static bool RequiresAntiforgery(HttpMethod method) =>
        method != HttpMethod.Get &&
        method != HttpMethod.Head &&
        method != HttpMethod.Options &&
        method != HttpMethod.Trace;
  
}
