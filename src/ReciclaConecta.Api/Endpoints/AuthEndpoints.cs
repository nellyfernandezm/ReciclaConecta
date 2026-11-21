using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReciclaConecta.Api.Data;
using ReciclaConecta.Api.Domain;
using ReciclaConecta.Api.Security;

namespace ReciclaConecta.Api.Endpoints;

public class RegistroDto
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = "";
    [Required, EmailAddress(ErrorMessage = "El correo no es válido."), StringLength(150)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = "";
    [Required]
    public string Rol { get; set; } = Roles.Ciudadano;
    [StringLength(60)]
    public string Distrito { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

// HU01 Registro y HU02 Inicio de sesión.
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/registro", async (RegistroDto dto, AppDbContext db, IPasswordHasher<Usuario> hasher) =>
        {
            Validacion.Validar(dto);
            // Por registro público solo se crean ciudadanos o recolectores (nunca Admin).
            if (dto.Rol != Roles.Ciudadano && dto.Rol != Roles.Recolector)
                throw new ValidacionException("El rol debe ser Ciudadano o Recolector.");
            if (!ReglasUsuario.PasswordValida(dto.Password))
                throw new ValidacionException("La contraseña debe tener al menos 8 caracteres, con letras y números.");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await db.Usuarios.AnyAsync(u => u.Email == email))
                return Results.Conflict(new { error = "Ya existe una cuenta con ese correo." });

            var u = new Usuario { Nombre = dto.Nombre.Trim(), Email = email, Rol = dto.Rol, Distrito = dto.Distrito.Trim() };
            u.PasswordHash = hasher.HashPassword(u, dto.Password);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            return Results.Created($"/api/usuarios/{u.Id}", new { u.Id, u.Nombre, u.Email, u.Rol, u.Verificado });
        });

        g.MapPost("/login", async (LoginDto dto, AppDbContext db, IPasswordHasher<Usuario> hasher, JwtConfig jwt) =>
        {
            Validacion.Validar(dto);
            var email = dto.Email.Trim().ToLowerInvariant();
            var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
            // Mismo mensaje si no existe o si la clave es incorrecta (no revela qué correos existen).
            if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
                return Results.Json(new { error = "Correo o contraseña incorrectos." }, statusCode: 401);
            if (!u.Activo)
                return Results.Json(new { error = "La cuenta está desactivada." }, statusCode: 403);
            return Results.Ok(new { token = jwt.CrearToken(u), nombre = u.Nombre, rol = u.Rol, verificado = u.Verificado });
        }).RequireRateLimiting("login");
    }
}
