using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ReciclaConecta.Api.Data;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Endpoints;

public class HorarioDto
{
    [Range(0, 6)] public int DiaSemana { get; set; }
    [Required] public string HoraInicio { get; set; } = "08:00";
    [Required] public string HoraFin { get; set; } = "18:00";
}

public class PuntoDto
{
    [Required, StringLength(120, MinimumLength = 3)] public string Nombre { get; set; } = "";
    [Required, StringLength(200)] public string Direccion { get; set; } = "";
    [Required, StringLength(60)] public string Distrito { get; set; } = "";
    [Range(-90, 90)] public double Lat { get; set; }
    [Range(-180, 180)] public double Lng { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [Required, MinLength(1, ErrorMessage = "Indica al menos un material.")] public List<string> Materiales { get; set; } = new();
    public List<HorarioDto> Horarios { get; set; } = new();
}

// HU04-HU06 consulta pública de puntos; HU07 mantenimiento (Admin).
public static class PuntosEndpoints
{
    public static void MapPuntos(this WebApplication app)
    {
        app.MapGet("/api/materiales", async (AppDbContext db) =>
            await db.Materiales.OrderBy(m => m.Id).Select(m => new { m.Id, m.Codigo, m.Nombre }).ToListAsync());

        var g = app.MapGroup("/api/puntos");

        g.MapGet("/", async (AppDbContext db, string? material, double? lat, double? lng, bool? abiertoAhora) =>
        {
            var query = Consulta(db).Where(p => p.Activo);
            if (!string.IsNullOrWhiteSpace(material))
            {
                var cod = material.Trim().ToUpperInvariant();
                query = query.Where(p => p.Materiales.Any(m => m.Material!.Codigo == cod));
            }
            var ahoraLima = HorarioLima.Ahora();
            var lista = (await query.ToListAsync()).Select(p => Vista(p, ahoraLima, lat, lng));
            if (abiertoAhora == true) lista = lista.Where(p => p.AbiertoAhora);
            // Si el usuario envía su ubicación, se ordenan del más cercano al más lejano.
            return lat.HasValue && lng.HasValue ? lista.OrderBy(p => p.DistanciaKm).ToList() : lista.OrderBy(p => p.Nombre).ToList();
        });

        g.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var p = await Consulta(db).FirstOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound(new { error = "Punto no encontrado." }) : Results.Ok(Vista(p, HorarioLima.Ahora(), null, null));
        });

        g.MapPost("/", async (PuntoDto dto, AppDbContext db) =>
        {
            var p = new PuntoAcopio();
            await Aplicar(p, dto, db);
            db.Puntos.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/puntos/{p.Id}", new { p.Id });
        }).RequireAuthorization(r => r.RequireRole(Roles.Admin));

        g.MapPut("/{id:int}", async (int id, PuntoDto dto, AppDbContext db) =>
        {
            var p = await Consulta(db).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound(new { error = "Punto no encontrado." });
            db.RemoveRange(p.Materiales);
            db.RemoveRange(p.Horarios);
            await Aplicar(p, dto, db);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(r => r.RequireRole(Roles.Admin));

        // Borrado lógico: el punto deja de mostrarse pero se conserva el historial.
        g.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var p = await db.Puntos.FindAsync(id);
            if (p is null) return Results.NotFound(new { error = "Punto no encontrado." });
            p.Activo = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(r => r.RequireRole(Roles.Admin));
    }

    private static IQueryable<PuntoAcopio> Consulta(AppDbContext db) =>
        db.Puntos.Include(p => p.Materiales).ThenInclude(m => m.Material).Include(p => p.Horarios);

    public record PuntoVista(int Id, string Nombre, string Direccion, string Distrito, double Lat, double Lng, string? Telefono,
        bool Activo, List<string> Materiales, List<string> MaterialesNombre, List<object> Horarios, bool AbiertoAhora, double? DistanciaKm);

    private static PuntoVista Vista(PuntoAcopio p, DateTime ahoraLima, double? lat, double? lng) => new(
        p.Id, p.Nombre, p.Direccion, p.Distrito, p.Lat, p.Lng, p.Telefono, p.Activo,
        p.Materiales.Select(m => m.Material!.Codigo).ToList(),
        p.Materiales.Select(m => m.Material!.Nombre).ToList(),
        p.Horarios.OrderBy(h => h.DiaSemana).Select(h => (object)new { h.DiaSemana, HoraInicio = h.HoraInicio.ToString("HH:mm"), HoraFin = h.HoraFin.ToString("HH:mm") }).ToList(),
        HorarioLima.EstaAbierto(p.Horarios, ahoraLima),
        lat.HasValue && lng.HasValue ? Math.Round(Geo.DistanciaKm(lat.Value, lng.Value, p.Lat, p.Lng), 2) : null);

    private static async Task Aplicar(PuntoAcopio p, PuntoDto dto, AppDbContext db)
    {
        Validacion.Validar(dto);
        var codigos = dto.Materiales.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        var mats = await db.Materiales.Where(m => codigos.Contains(m.Codigo)).ToListAsync();
        if (mats.Count != codigos.Count) throw new ValidacionException("Hay materiales no válidos.");

        var horarios = new List<Horario>();
        foreach (var h in dto.Horarios)
        {
            Validacion.Validar(h);
            if (!TimeOnly.TryParse(h.HoraInicio, out var ini) || !TimeOnly.TryParse(h.HoraFin, out var fin) || fin <= ini)
                throw new ValidacionException("Horario no válido: la hora de fin debe ser mayor que la de inicio.");
            horarios.Add(new Horario { DiaSemana = h.DiaSemana, HoraInicio = ini, HoraFin = fin });
        }

        p.Nombre = dto.Nombre.Trim();
        p.Direccion = dto.Direccion.Trim();
        p.Distrito = dto.Distrito.Trim();
        p.Lat = dto.Lat;
        p.Lng = dto.Lng;
        p.Telefono = dto.Telefono?.Trim();
        p.Activo = true;
        p.Materiales = mats.Select(m => new PuntoMaterial { MaterialId = m.Id }).ToList();
        p.Horarios = horarios;
    }
}
