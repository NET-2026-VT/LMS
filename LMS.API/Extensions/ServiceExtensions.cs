using LMS.Infrastructure.Repositories;
using LMS.Infrastructure.Services;
using LMS.Services;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;

namespace LMS.API.Extensions;

public static class ServiceExtensions
{
    public static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    public static void AddServiceLayer(this IServiceCollection services)
    {
        services.AddScoped<IServiceManager, ServiceManager>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddLazy<IAuthService>();
    }
}
public static class OtherServicesExtensions
{
    public static void ConfigureSwagger(this IServiceCollection services) =>
               services.AddEndpointsApiExplorer()
              .AddSwaggerGen(setup =>
              {
                  setup.EnableAnnotations();

                  setup.SwaggerDoc("v1", new OpenApiInfo
                  {
                      Title = "LMS API",
                      Version = "v1"
                  });


                  setup.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
                  {
                      In = ParameterLocation.Header,
                      Description = "Paste in JWT access token",
                      Name = "Authorization",
                      Type = SecuritySchemeType.Http,
                      BearerFormat = "JWT",
                      Scheme = "bearer"
                  });

                  setup.OperationFilter<SecurityRequirementsOperationFilter>(true, "bearer");

              });
}
