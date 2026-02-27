using backend;
using backend.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backend.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly string _jwtKey = "THIS_IS_A_SUPER_LONG_DEV_SECRET_KEY_1234567890";

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public string? Login(string username, string password)
    {
        var user = _db.Users.FirstOrDefault(u => u.Username == username);

        if (user == null) return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    

var claims = new[]
{
    new Claim("id", user.Id.ToString()),
    new Claim(ClaimTypes.Name, user.Username),
    new Claim(ClaimTypes.Role, user.Role)
};

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}