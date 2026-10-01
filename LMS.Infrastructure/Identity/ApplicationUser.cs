using Domain.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace LMS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public User DomainUser { get; set; } = null!;

    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpireTime { get; set; }
}
