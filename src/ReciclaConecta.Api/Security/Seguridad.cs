using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Security;

public class JwtConfig
{
    public string Emisor { get; init; } = "ReciclaConecta";
    public SymmetricSecurityKey Clave { get; init; } = null!;

    // La clave viene de la variable de entorno Jwt__Key. Si no existe se genera una aleatoria
    // (los tokens dejan de valer al reiniciar). Nunca se guarda una clave en el repositorio.
    public static JwtConfig Desde(IConfiguration config)
    {
        var texto = config["Jwt:Key"];
        byte[] bytes = string.IsNullOrWhiteSpace(texto) || texto.Length < 32
            ? RandomNumberGenerator.GetBytes(64)
            : Encoding.UTF8.GetBytes(texto);
        return new JwtConfig { Clave = new SymmetricSecurityKey(bytes) };
    }

    public string CrearToken(Usuario u)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nombre),
            new Claim(ClaimTypes.Role, u.Rol),
        };
        var token = new JwtSecurityToken(Emisor, Emisor, claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(Clave, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class UsuarioActual
{
    public static int Id(this ClaimsPrincipal p) => int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// Cabeceras de seguridad en todas las respuestas (OWASP Secure Headers).
public class CabecerasSeguridadMiddleware(RequestDelegate next)
{
    public const string Csp =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com; " +
        "style-src 'self' https://unpkg.com; " +
        "img-src 'self' data: https://tile.openstreetmap.org https://unpkg.com; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["Content-Security-Policy"] = Csp;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
        return next(ctx);
    }
}
