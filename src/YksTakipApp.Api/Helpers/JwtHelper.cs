using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YksTakipApp.Core.Entities;

namespace YksTakipApp.Api.Helpers
{
    public static class JwtHelper
    {
        public static string GenerateToken(User user, IConfiguration config)
        {
            var jwtSettings = config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Hem "sub" hem ClaimTypes.NameIdentifier
            var role = "User"; // TODO: İhtiyaca göre veri kaynağından alınabilir
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // standard subject
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),   // ASP.NET standard
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
