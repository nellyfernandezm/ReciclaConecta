using Microsoft.EntityFrameworkCore;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Material> Materiales => Set<Material>();
    public DbSet<PuntoAcopio> Puntos => Set<PuntoAcopio>();
    public DbSet<Horario> Horarios => Set<Horario>();
    public DbSet<SolicitudRecojo> Solicitudes => Set<SolicitudRecojo>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Usuario>().Property(u => u.Email).HasMaxLength(150);
        b.Entity<Usuario>().Property(u => u.Nombre).HasMaxLength(100);
        b.Entity<Material>().HasIndex(m => m.Codigo).IsUnique();
        b.Entity<PuntoMaterial>().HasKey(pm => new { pm.PuntoId, pm.MaterialId });
        b.Entity<PuntoAcopio>().HasMany(p => p.Materiales).WithOne().HasForeignKey(pm => pm.PuntoId);
        b.Entity<PuntoAcopio>().HasMany(p => p.Horarios).WithOne().HasForeignKey(h => h.PuntoId);
        // Estado como token de concurrencia: si dos recolectores aceptan a la vez, el segundo falla (409).
        b.Entity<SolicitudRecojo>().Property(s => s.Estado).HasConversion<string>().IsConcurrencyToken();
        b.Entity<SolicitudRecojo>().HasOne(s => s.Ciudadano).WithMany().HasForeignKey(s => s.CiudadanoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SolicitudRecojo>().HasOne(s => s.Recolector).WithMany().HasForeignKey(s => s.RecolectorId).OnDelete(DeleteBehavior.Restrict);
    }
}
