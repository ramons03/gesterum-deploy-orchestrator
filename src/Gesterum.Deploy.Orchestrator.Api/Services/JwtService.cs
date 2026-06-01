using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class JwtService
{
    private readonly JwtOptions _opt;

    public JwtService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
    }

    public (string token, DateTime expiresAtUtc) CreateToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? "user")
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }
}
