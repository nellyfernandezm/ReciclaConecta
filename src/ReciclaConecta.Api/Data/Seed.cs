using Microsoft.AspNetCore.Identity;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Data;

// Datos iniciales de demostración: se cargan solo si la base está vacía.
public static class Seed
{
    // Credencial de DEMOSTRACIÓN (documentada en el README). No es un secreto.
    public const string PasswordDemo = "Demo2026!";

    public static void Cargar(AppDbContext db, IPasswordHasher<Usuario> hasher)
    {
        if (db.Materiales.Any()) return;

        var materiales = new[]
        {
            new Material { Codigo = "PAPEL", Nombre = "Papel" },
            new Material { Codigo = "CARTON", Nombre = "Cartón" },
            new Material { Codigo = "PLASTICO", Nombre = "Plástico" },
            new Material { Codigo = "VIDRIO", Nombre = "Vidrio" },
            new Material { Codigo = "METAL", Nombre = "Metal" },
            new Material { Codigo = "RAEE", Nombre = "Aparatos electrónicos (RAEE)" },
        };
        db.Materiales.AddRange(materiales);
        db.SaveChanges();
        int M(string c) => materiales.First(m => m.Codigo == c).Id;

        // Puntos de acopio de ejemplo en Lima (nombres genéricos, coordenadas aproximadas).
        var puntos = new List<PuntoAcopio>
        {
            Punto("Punto Limpio Parque Kennedy", "Av. Larco cdra. 1", "Miraflores", -12.1211, -77.0297, "PAPEL,CARTON,PLASTICO,VIDRIO", LunSab(8, 18)),
            Punto("Ecopunto Olivar", "Calle Los Olivos 300", "San Isidro", -12.0986, -77.0353, "PAPEL,CARTON,PLASTICO,METAL", LunVie(9, 17)),
            Punto("Centro de Acopio Surco", "Av. Caminos del Inca 1500", "Santiago de Surco", -12.1355, -76.9930, "PLASTICO,VIDRIO,METAL,RAEE", LunSab(7, 19)),
            Punto("Ecopunto Lince", "Av. Arequipa 2000", "Lince", -12.0850, -77.0340, "PAPEL,CARTON,PLASTICO", LunVie(8, 16)),
            Punto("Punto Verde Campo de Marte", "Av. Salaverry cdra. 1", "Jesús María", -12.0700, -77.0430, "PAPEL,CARTON,VIDRIO,METAL", TodosLosDias(7, 20)),
            Punto("Acopio RAEE Lima Centro", "Jr. Lampa 500", "Cercado de Lima", -12.0470, -77.0310, "RAEE,METAL", LunVie(9, 18)),
            Punto("Ecopunto Canto Grande", "Av. Próceres de la Independencia 2500", "San Juan de Lurigancho", -11.9800, -77.0050, "PAPEL,CARTON,PLASTICO,METAL", LunSab(8, 18)),
            Punto("Centro de Reciclaje Los Olivos", "Av. Carlos Izaguirre 900", "Los Olivos", -11.9900, -77.0700, "PLASTICO,VIDRIO,RAEE", LunSab(9, 17)),
            Punto("Punto Limpio Pueblo Libre", "Av. La Mar 1200", "Pueblo Libre", -12.0760, -77.0640, "PAPEL,CARTON,VIDRIO", LunVie(8, 14)),
            Punto("Ecopunto La Victoria", "Av. Aviación 1000", "La Victoria", -12.0820, -77.0010, "CARTON,PLASTICO,METAL", LunSab(6, 14)),
        };
        db.Puntos.AddRange(puntos);
        db.SaveChanges();

        var admin = NuevoUsuario("Administradora ReciclaConecta", "admin@reciclaconecta.pe", Roles.Admin, "Miraflores", true);
        var ciudadano = NuevoUsuario("Lucía Ramos", "ciudadano@reciclaconecta.pe", Roles.Ciudadano, "Miraflores", false);
        var recolector = NuevoUsuario("Jorge Quispe", "recolector@reciclaconecta.pe", Roles.Recolector, "Surquillo", true);
        var recolector2 = NuevoUsuario("Rosa Huamán", "recolector2@reciclaconecta.pe", Roles.Recolector, "Lince", false);
        db.Usuarios.AddRange(admin, ciudadano, recolector, recolector2);
        db.SaveChanges();

        var ahora = DateTime.UtcNow;
        db.Solicitudes.AddRange(
            new SolicitudRecojo { CiudadanoId = ciudadano.Id, MaterialId = M("CARTON"), CantidadEstimadaKg = 8, Direccion = "Calle Schell 250", Distrito = "Miraflores", Lat = -12.1195, Lng = -77.0320, Referencia = "Portería, preguntar por Lucía", FechaCreacion = ahora.AddHours(-3) },
            new SolicitudRecojo { CiudadanoId = ciudadano.Id, MaterialId = M("PLASTICO"), CantidadEstimadaKg = 5, Direccion = "Av. Benavides 400", Distrito = "Miraflores", Lat = -12.1260, Lng = -77.0270, FechaCreacion = ahora.AddHours(-1) },
            new SolicitudRecojo { CiudadanoId = ciudadano.Id, MaterialId = M("PAPEL"), CantidadEstimadaKg = 12, Direccion = "Calle Schell 250", Distrito = "Miraflores", Lat = -12.1195, Lng = -77.0320, Estado = EstadoSolicitud.Aceptada, RecolectorId = recolector.Id, FechaCreacion = ahora.AddDays(-1), FechaAceptacion = ahora.AddHours(-20) },
            new SolicitudRecojo { CiudadanoId = ciudadano.Id, MaterialId = M("VIDRIO"), CantidadEstimadaKg = 6, KgReales = 7.5, Direccion = "Calle Schell 250", Distrito = "Miraflores", Lat = -12.1195, Lng = -77.0320, Estado = EstadoSolicitud.Recogida, RecolectorId = recolector.Id, FechaCreacion = ahora.AddDays(-5), FechaAceptacion = ahora.AddDays(-5), FechaRecojo = ahora.AddDays(-4), Calificacion = 5, Comentario = "Muy puntual" },
            new SolicitudRecojo { CiudadanoId = ciudadano.Id, MaterialId = M("METAL"), CantidadEstimadaKg = 3, KgReales = 2.8, Direccion = "Calle Schell 250", Distrito = "Miraflores", Lat = -12.1195, Lng = -77.0320, Estado = EstadoSolicitud.Recogida, RecolectorId = recolector.Id, FechaCreacion = ahora.AddDays(-3), FechaAceptacion = ahora.AddDays(-3), FechaRecojo = ahora.AddDays(-2) }
        );
        db.SaveChanges();

        PuntoAcopio Punto(string nombre, string dir, string distrito, double lat, double lng, string mats, List<Horario> horarios) => new()
        {
            Nombre = nombre, Direccion = dir, Distrito = distrito, Lat = lat, Lng = lng, Telefono = "01 000 0000",
            Materiales = mats.Split(',').Select(c => new PuntoMaterial { MaterialId = M(c) }).ToList(),
            Horarios = horarios
        };

        Usuario NuevoUsuario(string nombre, string email, string rol, string distrito, bool verificado)
        {
            var u = new Usuario { Nombre = nombre, Email = email, Rol = rol, Distrito = distrito, Verificado = verificado };
            u.PasswordHash = hasher.HashPassword(u, PasswordDemo);
            return u;
        }
    }

    private static List<Horario> Dias(IEnumerable<int> dias, int ini, int fin) =>
        dias.Select(d => new Horario { DiaSemana = d, HoraInicio = new TimeOnly(ini, 0), HoraFin = new TimeOnly(fin, 0) }).ToList();
    private static List<Horario> LunVie(int ini, int fin) => Dias(Enumerable.Range(1, 5), ini, fin);
    private static List<Horario> LunSab(int ini, int fin) => Dias(Enumerable.Range(1, 6), ini, fin);
    private static List<Horario> TodosLosDias(int ini, int fin) => Dias(Enumerable.Range(0, 7), ini, fin);
}
