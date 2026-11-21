namespace ReciclaConecta.Api.Domain;

// Roles del sistema (se guardan como texto en el token JWT).
public static class Roles
{
    public const string Ciudadano = "Ciudadano";
    public const string Recolector = "Recolector";
    public const string Admin = "Admin";
}

public enum EstadoSolicitud { Pendiente, Aceptada, Recogida, Cancelada }

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = Roles.Ciudadano;
    public string Distrito { get; set; } = "";
    public bool Activo { get; set; } = true;
    // Solo aplica a recolectores: el admin lo verifica antes de que pueda aceptar recojos.
    public bool Verificado { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

public class Material
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";   // PAPEL, CARTON, PLASTICO, VIDRIO, METAL, RAEE
    public string Nombre { get; set; } = "";
}

public class PuntoAcopio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Distrito { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public List<PuntoMaterial> Materiales { get; set; } = new();
    public List<Horario> Horarios { get; set; } = new();
}

public class PuntoMaterial
{
    public int PuntoId { get; set; }
    public int MaterialId { get; set; }
    public Material? Material { get; set; }
}

public class Horario
{
    public int Id { get; set; }
    public int PuntoId { get; set; }
    public int DiaSemana { get; set; }        // 0 = domingo ... 6 = sábado
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
}

public class SolicitudRecojo
{
    public int Id { get; set; }
    public int CiudadanoId { get; set; }
    public Usuario? Ciudadano { get; set; }
    public int MaterialId { get; set; }
    public Material? Material { get; set; }
    public double CantidadEstimadaKg { get; set; }
    public double? KgReales { get; set; }
    public string Direccion { get; set; } = "";
    public string Distrito { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Referencia { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public int? RecolectorId { get; set; }
    public Usuario? Recolector { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaAceptacion { get; set; }
    public DateTime? FechaRecojo { get; set; }
    public int? Calificacion { get; set; }
    public string? Comentario { get; set; }
}
