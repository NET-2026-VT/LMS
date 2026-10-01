namespace LMS.Blazor.Client.Services.ApiProxy;

public static class ApiProxyPath
{
    public static string Validate(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) ||
            endpoint.StartsWith('/') ||
            endpoint.StartsWith('\\') ||
            endpoint.Contains('\\') ||
            endpoint.Contains('#') ||
            Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                "A non-empty relative API path is required.",
                nameof(endpoint));
        }

        var path = endpoint.Split('?', 2)[0];
        if (Uri.UnescapeDataString(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                "Relative API paths cannot contain traversal segments.",
                nameof(endpoint));
        }

        return endpoint;
    }
}
