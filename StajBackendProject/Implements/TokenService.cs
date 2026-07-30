using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;

namespace StajBackendProject.Implements
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UsersContext _context;

        public TokenService(IConfiguration configuration, UsersContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FirstName)
            };

            if (user.Roles != null)
            {
                foreach (var role in user.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Name));
                }
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public TokenResponseDto RefreshToken(RefreshTokenRequestDto request)
        {
            var session = _context.UserSessions
                .Include(s => s.User)
                .ThenInclude(u => u.Roles)
                .SingleOrDefault(s => s.RefreshToken == request.RefreshToken && !s.IsRevoked);

            if (session == null || session.ExpiryDate <= DateTime.UtcNow)
            {
                throw new Exception("Invalid or expired refresh token. Please login again.");
            }

            var newAccessToken = GenerateJwtToken(session.User);
            var newRefreshToken = GenerateRefreshToken();

            session.IsRevoked = true;

            var newSession = new UserSession
            {
                UserId = session.UserId,
                RefreshToken = newRefreshToken,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                IpAddress = session.IpAddress,
                DeviceName = session.DeviceName,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserSessions.Add(newSession);
            _context.SaveChanges();

            return new TokenResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

        public string GenerateEmailVerificationToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToHexString(randomNumber);
        }
    }
}