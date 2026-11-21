using Microsoft.EntityFrameworkCore;
using ReciclaConecta.Api.Data;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Endpoints;

public class ActivoDto { public bool Activo { get; set; } }

// HU03 gestión de usuarios y HU15 panel de indicadores (solo Admin).
public static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        var usuarios = app.MapGroup("/api/usuarios").RequireAuthorization(r => r.RequireRole(Roles.Admin));

        usuarios.MapGet("/", async (AppDbContext db) =>
            await db.Usuarios.OrderBy(u => u.Rol).ThenBy(u => u.Nombre)
                .Select(u => new { u.Id, u.Nombre, u.Email, u.Rol, u.Distrito, u.Activo, u.Verificado, u.FechaRegistro })
                .ToListAsync());

        // El admin verifica a un recolector (requisito para aceptar recojos).
        usuarios.MapPut("/{id:int}/verificar", async (int id, AppDbContext db) =>
        {
            var u = await db.Usuarios.FindAsync(id);
            if (u is null) return Results.NotFound(new { error = "Usuario no encontrado." });
            if (u.Rol != Roles.Recolector) return Results.BadRequest(new { error = "Solo se verifican recolectores." });
            u.Verificado = true;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        usuarios.MapPut("/{id:int}/activo", async (int id, ActivoDto dto, AppDbContext db) =>
        {
            var u = await db.Usuarios.FindAsync(id);
            if (u is null) return Results.NotFound(new { error = "Usuario no encontrado." });
            if (u.Rol == Roles.Admin && !dto.Activo) return Results.BadRequest(new { error = "No se puede desactivar a un administrador." });
            u.Activo = dto.Activo;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/api/admin/indicadores", async (AppDbContext db) =>
        {
            var solicitudes = await db.Solicitudes.Include(s => s.Material).ToListAsync();
            var recogidas = solicitudes.Where(s => s.Estado == EstadoSolicitud.Recogida).ToList();
            return new
            {
                totalKg = Math.Round(recogidas.Sum(s => s.KgReales ?? 0), 2),
                totalSolicitudes = solicitudes.Count,
                kgPorMaterial = recogidas.GroupBy(s => s.Material!.Nombre)
                    .Select(g => new { material = g.Key, kg = Math.Round(g.Sum(s => s.KgReales ?? 0), 2) })
                    .OrderByDescending(x => x.kg).ToList(),
                solicitudesPorEstado = Enum.GetValues<EstadoSolicitud>()
                    .Select(e => new { estado = e.ToString(), cantidad = solicitudes.Count(s => s.Estado == e) }).ToList(),
                recolectoresActivos = await db.Usuarios.CountAsync(u => u.Rol == Roles.Recolector && u.Activo && u.Verificado),
                recolectoresPorVerificar = await db.Usuarios.CountAsync(u => u.Rol == Roles.Recolector && !u.Verificado),
                puntosActivos = await db.Puntos.CountAsync(p => p.Activo),
                topDistritos = solicitudes.GroupBy(s => s.Distrito)
                    .Select(g => new { distrito = g.Key, solicitudes = g.Count() })
                    .OrderByDescending(x => x.solicitudes).Take(5).ToList(),
                calificacionPromedio = recogidas.Where(s => s.Calificacion.HasValue).Select(s => (double)s.Calificacion!.Value)
                    .DefaultIfEmpty(0).Average()
            };
        }).RequireAuthorization(r => r.RequireRole(Roles.Admin));
    }
}
