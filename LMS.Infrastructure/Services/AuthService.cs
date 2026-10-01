using Domain.Models.Configurations;
using Domain.Models.Exceptions;
using LMS.Infrastructure.Identity;
using LMS.Shared.DTOs.AuthDtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Service.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LMS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<IdentityRole> roleManager;
    private JwtSettings jwtSettings;
    private ApplicationUser? user;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<JwtSettings> jwtSettings)
    {
        this.userManager = userManager;
        this.roleManager = roleManager;
        this.jwtSettings = jwtSettings.Value;
    }

    public async Task<TokenDto> CreateTokenAsync(bool addTime)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));

        SigningCredentials signing = GetSigningCredentials();
        IEnumerable<Claim> claims = await GetClaimsAsync();
        JwtSecurityToken token = GenerateToken(signing, claims);

        user.RefreshToken = GenerateRefreshToken();

        if (addTime)
            user.RefreshTokenExpireTime = DateTime.UtcNow.AddDays(10);

        var res = await userManager.UpdateAsync(user);
        if (!res.Succeeded) throw new Exception
                (string.Join("\n", res.Errors.Select(e => $"{e.Code}: {e.Description}")));

        return new TokenDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.RefreshToken,
            new DateTimeOffset(token.ValidTo, TimeSpan.Zero),
            new DateTimeOffset(
                DateTime.SpecifyKind(user.RefreshTokenExpireTime, DateTimeKind.Utc)));
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private JwtSecurityToken GenerateToken(SigningCredentials signing, IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
                                    issuer: jwtSettings.Issuer,
                                    audience: jwtSettings.Audience,
                                    claims: claims,
                                    expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings.Expires)),
                                    signingCredentials: signing);

        return token;
    }

    private async Task<IEnumerable<Claim>> GetClaimsAsync()
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName!)
            //Add more if you need
        };

        var roles = await userManager.GetRolesAsync(user);

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;

    }

    private SigningCredentials GetSigningCredentials()
    {

        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);
        var secret = new SymmetricSecurityKey(key);

        return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
    }

    public async Task<bool> ValidateUserAsync(UserAuthDto userDto)
    {
        ArgumentNullException.ThrowIfNull(userDto);

        user = await userManager.FindByEmailAsync(userDto.Email);

        return user != null && await userManager.CheckPasswordAsync(user, userDto.Password);
    }

    public async Task<TokenDto> RefreshTokenAsync(TokenDto token)
    {
        ClaimsPrincipal principal = GetPrincipalFromExpiredToken(token.AccessToken);
        ApplicationUser? user = await userManager.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (user == null || user.RefreshToken != token.RefreshToken || user.RefreshTokenExpireTime <= DateTime.UtcNow)
            throw new TokenValidationException();

        this.user = user;

        return await CreateTokenAsync(addTime: false);
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken)
    {

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        ClaimsPrincipal principal = tokenHandler.ValidateToken(accessToken, tokenValidationParameters, out SecurityToken securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }

        return principal;
    }
}
