using LMS.Blazor.Components;
using LMS.Blazor.Extensions;
using LMS.Blazor.Services.Authentication.Tokens;

namespace LMS.Blazor;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddInteractiveServerComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddControllersWithViews();
        builder.Services.AddBffAuthentication(builder.Configuration);
        builder.Services.AddBffAntiforgery();
        builder.Services.AddRemoteApiProxy(builder.Configuration);

        var app = builder.Build();

        // Load and validate persisted sessions before the first request arrives.
        // Forces it to get an instance before app starts
        _ = app.Services.GetRequiredService<ITokenStorage>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        // A plain 404 does not restore Blazor's persisted auth state.
        // Redirecting to a real routed page returns a normal 200 and keeps the user's
        // serialized authentication state from the cookie.
        // Used when a page (route) is not-found
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api"),
            branch => branch.UseStatusCodePagesWithRedirects("/not-found"));
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapControllers();
        app.MapAntiforgeryTokenEndpoint();
        app.MapRemoteApiProxy();
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.Run();
    }
}
