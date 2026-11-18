using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Cs2D.Server;

public record UserRecord(string Id, string Name, string Salt, string Hash);
public record Credentials(string Username, string Password);

/// <summary>Simple JSON-file user store (swap for a real DB later).</summary>
public class UserStore
{
    readonly string path = Path.Combine(AppContext.BaseDirectory, "users.json");
    readonly object gate = new();
    List<UserRecord> users;
    public UserStore() { users = File.Exists(path) ? JsonSerializer.Deserialize<List<UserRecord>>(File.ReadAllText(path)) ?? [] : []; }

    static string Hash(string pw, string salt) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(pw, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256, 32));

    public UserRecord? Register(string name, string pw)
    {
        lock (gate)
        {
            if (users.Any(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return null;
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var u = new UserRecord(Guid.NewGuid().ToString("N"), name, salt, Hash(pw, salt));
            users.Add(u); File.WriteAllText(path, JsonSerializer.Serialize(users)); return u;
        }
    }
    public UserRecord? Login(string name, string pw)
    {
        lock (gate)
        {
            var u = users.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return u != null && CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Hash(pw, u.Salt)), Convert.FromBase64String(u.Hash)) ? u : null;
        }
    }
}

public static class Jwt
{
    public static SymmetricSecurityKey Key(IConfiguration c) =>
        new(Encoding.UTF8.GetBytes(c["Jwt:Key"] ?? "dev-only-secret-key-change-me-please-32bytes+"));

    public static string Create(UserRecord u, IConfiguration c) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, u.Id), new Claim(ClaimTypes.Name, u.Name)],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(Key(c), SecurityAlgorithms.HmacSha256)));
}
