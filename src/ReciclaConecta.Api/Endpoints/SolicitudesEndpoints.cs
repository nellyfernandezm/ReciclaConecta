using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ReciclaConecta.Api.Data;
using ReciclaConecta.Api.Domain;
using ReciclaConecta.Api.Security;

namespace ReciclaConecta.Api.Endpoints;

public class SolicitudDto
{
    [Required] public string Material { get; set; } = "";
    public double CantidadEstimadaKg { get; set; }
    [Required(ErrorMessage = "La dirección es obligatoria."), StringLength(200, MinimumLength = 5)] public string Direccion { get; set; } = "";
    [Required, StringLength(60)] public string Distrito { get; set; } = "";
    [Range(-90, 90)] public double Lat { get; set; }
    [Range(-180, 180)] public double Lng { get; set; }
    [StringLength(200)] public string? Referencia { get; set; }
}

public class RecojoDto { public double KgReales { get; set; } }
public class CalificacionDto { public int Calificacion { get; set; } [StringLength(300)] public string? Comentario { get; set; } }

// HU08-HU14: ciclo de vida de una solicitud de recojo.
public static class SolicitudesEndpoints
{
    public static void MapSolicitudes(this WebApplication app)
    {
        var g = app.MapGroup("/api/solicitudes").RequireAuthorization();

        // HU08 El ciudadano publica una solicitud de recojo.
        g.MapPost("/", async (SolicitudDto dto, ClaimsPrincipal user, AppDbContext db) =>
        {
            Validacion.Validar(dto);
            var mat = await db.Materiales.FirstOrDefaultAsync(m => m.Codigo == dto.Material.Trim().ToUpper())
                      ?? throw new ValidacionException("Material no válido.");
            int id = user.Id();
            int pendientes = await db.Solicitudes.CountAsync(s => s.CiudadanoId == id && s.Estado == EstadoSolicitud.Pendiente);
            ReglasSolicitud.ValidarNueva(dto.CantidadEstimadaKg, pendientes);

            var s = new SolicitudRecojo
            {
                CiudadanoId = id, MaterialId = mat.Id, CantidadEstimadaKg = dto.CantidadEstimadaKg,
                Direccion = dto.Direccion.Trim(), Distrito = dto.Distrito.Trim(), Lat = dto.Lat, Lng = dto.Lng,
                Referencia = dto.Referencia?.Trim()
            };
            db.Solicitudes.Add(s);
            await db.SaveChangesAsync();
            return Results.Created($"/api/solicitudes/{s.Id}", new { s.Id, Estado = s.Estado.ToString() });
        }).RequireAuthorization(r => r.RequireRole(Roles.Ciudadano));

        // HU12/HU13 Ciudadano: sus solicitudes. Recolector: los recojos que aceptó.
        g.MapGet("/mias", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            int id = user.Id();
            var q = Consulta(db);
            q = user.IsInRole(Roles.Recolector) ? q.Where(s => s.RecolectorId == id) : q.Where(s => s.CiudadanoId == id);
            var lista = await q.OrderByDescending(s => s.FechaCreacion).ToListAsync();
            return lista.Select(s => Vista(s, null, null));
        });

        // HU09 El recolector ve las solicitudes pendientes cerca de él.
        g.MapGet("/disponibles", async (AppDbContext db, double? lat, double? lng, double? radioKm, string? material) =>
        {
            var q = Consulta(db).Where(s => s.Estado == EstadoSolicitud.Pendiente);
            if (!string.IsNullOrWhiteSpace(material)) q = q.Where(s => s.Material!.Codigo == material.Trim().ToUpper());
            var lista = (await q.ToListAsync()).Select(s => Vista(s, lat, lng));
            if (lat.HasValue && lng.HasValue)
            {
                double radio = Math.Clamp(radioKm ?? 10, 1, 50);
                lista = lista.Where(s => s.DistanciaKm <= radio).OrderBy(s => s.DistanciaKm);
            }
            return lista.ToList();
        }).RequireAuthorization(r => r.RequireRole(Roles.Recolector));

        // HU10 Aceptar (solo uno puede aceptarla: la segunda vez responde 409).
        g.MapPost("/{id:int}/aceptar", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var s = await db.Solicitudes.FindAsync(id);
            if (s is null) return Results.NotFound(new { error = "Solicitud no encontrada." });
            var recolector = await db.Usuarios.FindAsync(user.Id());
            ReglasSolicitud.Aceptar(s, recolector!, DateTime.UtcNow);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "La solicitud ya fue aceptada por otro recolector." });
            }
            return Results.Ok(new { s.Id, Estado = s.Estado.ToString() });
        }).RequireAuthorization(r => r.RequireRole(Roles.Recolector));

        // HU11 Marcar como recogida con los kg reales.
        g.MapPost("/{id:int}/recoger", async (int id, RecojoDto dto, ClaimsPrincipal user, AppDbContext db) =>
        {
            var s = await db.Solicitudes.FindAsync(id);
            if (s is null) return Results.NotFound(new { error = "Solicitud no encontrada." });
            ReglasSolicitud.Recoger(s, user.Id(), dto.KgReales, DateTime.UtcNow);
            await db.SaveChangesAsync();
            return Results.Ok(new { s.Id, Estado = s.Estado.ToString(), s.KgReales });
        }).RequireAuthorization(r => r.RequireRole(Roles.Recolector));

        // HU12 Cancelar una solicitud pendiente.
        g.MapPost("/{id:int}/cancelar", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var s = await db.Solicitudes.FindAsync(id);
            if (s is null) return Results.NotFound(new { error = "Solicitud no encontrada." });
            ReglasSolicitud.Cancelar(s, user.Id());
            await db.SaveChangesAsync();
            return Results.Ok(new { s.Id, Estado = s.Estado.ToString() });
        }).RequireAuthorization(r => r.RequireRole(Roles.Ciudadano));

        // HU14 Calificar al recolector.
        g.MapPost("/{id:int}/calificar", async (int id, CalificacionDto dto, ClaimsPrincipal user, AppDbContext db) =>
        {
            Validacion.Validar(dto);
            var s = await db.Solicitudes.FindAsync(id);
            if (s is null) return Results.NotFound(new { error = "Solicitud no encontrada." });
            ReglasSolicitud.Calificar(s, user.Id(), dto.Calificacion, dto.Comentario);
            await db.SaveChangesAsync();
            return Results.Ok(new { s.Id, s.Calificacion });
        }).RequireAuthorization(r => r.RequireRole(Roles.Ciudadano));

        // HU13 Impacto del ciudadano: kg reciclados por material.
        app.MapGet("/api/impacto/mio", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            int id = user.Id();
            var recogidas = await db.Solicitudes.Include(s => s.Material)
                .Where(s => s.CiudadanoId == id && s.Estado == EstadoSolicitud.Recogida).ToListAsync();
            var porMaterial = recogidas.GroupBy(s => s.Material!.Nombre)
                .Select(gr => new { material = gr.Key, kg = Math.Round(gr.Sum(s => s.KgReales ?? 0), 2) })
                .OrderByDescending(x => x.kg).ToList();
            return new
            {
                totalKg = Math.Round(recogidas.Sum(s => s.KgReales ?? 0), 2),
                solicitudesRecogidas = recogidas.Count,
                porMaterial
            };
        }).RequireAuthorization(r => r.RequireRole(Roles.Ciudadano));
    }

    private static IQueryable<SolicitudRecojo> Consulta(AppDbContext db) =>
        db.Solicitudes.Include(s => s.Material).Include(s => s.Ciudadano).Include(s => s.Recolector);

    // No se expone el correo ni datos internos de los usuarios: solo nombres.
    public record SolicitudVista(int Id, string Material, string MaterialNombre, double CantidadEstimadaKg, double? KgReales,
        string Direccion, string Distrito, double Lat, double Lng, string? Referencia, string Estado,
        string Ciudadano, string? Recolector, DateTime FechaCreacion, DateTime? FechaRecojo, int? Calificacion, string? Comentario, double? DistanciaKm);

    private static SolicitudVista Vista(SolicitudRecojo s, double? lat, double? lng) => new(
        s.Id, s.Material!.Codigo, s.Material.Nombre, s.CantidadEstimadaKg, s.KgReales, s.Direccion, s.Distrito, s.Lat, s.Lng,
        s.Referencia, s.Estado.ToString(), s.Ciudadano?.Nombre ?? "", s.Recolector?.Nombre, s.FechaCreacion, s.FechaRecojo,
        s.Calificacion, s.Comentario,
        lat.HasValue && lng.HasValue ? Math.Round(Geo.DistanciaKm(lat.Value, lng.Value, s.Lat, s.Lng), 2) : null);
}
