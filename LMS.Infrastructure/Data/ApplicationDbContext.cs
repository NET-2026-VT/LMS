using Domain.Models.Entities;
using LMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<User> DomainUsers => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(identityUser => identityUser.DomainUser)
            .WithOne()
            .HasForeignKey<ApplicationUser>(identityUser => identityUser.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
