using Domain.Models.Entities;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Services;

//ToDo: Add in UserSecrets (you can change password and secretkey):
//{
//  "password": "abc",
//  "JwtSettings": {
//    "secretkey" :  "ThisMustNeReallyLong!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
//  }
//}


internal class DataSeedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IConfiguration configuration;
    private readonly ILogger<DataSeedService> logger;
    private UserManager<ApplicationUser> userManager = null!;
    private RoleManager<IdentityRole> roleManager = null!;
    private string _password = null!;
    private const string DemoRole = "Demo";
    private const string DefaultUserEmail = "DemoUser@Lms.com";

    public DataSeedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DataSeedService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment()) return;

        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                            ?? throw new ArgumentNullException();

        if (await context.Users.AnyAsync(cancellationToken)) return;

        userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                            ?? throw new ArgumentNullException();

        roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
                            ?? throw new ArgumentNullException();

        _password = configuration["password"]!;
        ArgumentNullException.ThrowIfNull(_password, nameof(_password));

        try
        {
            await CreateRolesAsync([DemoRole]);
            await CreateDefaultUserAsync(context);
            logger.LogInformation("Seed complete");
        }
        catch (Exception ex)
        {
            logger.LogError($"Data seed fail with message: {ex.Message}. Exceeption: {ex.InnerException}");
            throw;
        }
    }


    private async Task CreateRolesAsync(string[] rolenames)
    {
        foreach (string rolename in rolenames)
        {
            if (await roleManager.RoleExistsAsync(rolename)) continue;
            var role = new IdentityRole { Name = rolename };
            var res = await roleManager.CreateAsync(role);

            if (!res.Succeeded) throw new Exception
                    (string.Join("\n", res.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }
    }
    private async Task CreateDefaultUserAsync(ApplicationDbContext context)
    {
        var user = new ApplicationUser
        {
            Email = DefaultUserEmail,
            UserName = DefaultUserEmail,
        };
        user.DomainUser = new User { Id = user.Id, Age = 0 };

        await CreateUserAsync(context, user, DemoRole);
    }

    private async Task CreateUserAsync(ApplicationDbContext context, ApplicationUser user, string role)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var result = await userManager.CreateAsync(user, _password);

        if (!result.Succeeded)
            throw new Exception(string.Join("\n",
            result.Errors.Select(e => $"{e.Code}: {e.Description}")));

        var roleResult = await userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
            throw new Exception(string.Join("\n",
            roleResult.Errors.Select(e => $"{e.Code}: {e.Description}")));

        await transaction.CommitAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}
