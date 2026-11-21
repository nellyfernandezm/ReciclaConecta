using System.Text.RegularExpressions;

namespace ReciclaConecta.Api.Domain;

// Error de negocio: la API lo convierte en 400 o 409 según Conflicto.
public class ReglaNegocioException(string mensaje, bool conflicto = false) : Exception(mensaje)
{
    public bool Conflicto { get; } = conflicto;
}

public static class Geo
{
    // Distancia en km entre dos coordenadas (fórmula de Haversine).
    public static double DistanciaKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double dLat = Rad(lat2 - lat1), dLng = Rad(lng2 - lng1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double g) => g * Math.PI / 180.0;
}

public static class HorarioLima
{
    // Lima no tiene horario de verano: UTC-5 fijo, así no dependemos de la zona del servidor.
    public static DateTime Ahora(DateTime? utc = null) => (utc ?? DateTime.UtcNow).AddHours(-5);

    // ¿El punto está abierto en esa fecha/hora de Lima? [inicio, fin)
    public static bool EstaAbierto(IEnumerable<Horario> horarios, DateTime fechaHoraLima)
    {
        int dia = (int)fechaHoraLima.DayOfWeek;
        var hora = TimeOnly.FromDateTime(fechaHoraLima);
        return horarios.Any(h => h.DiaSemana == dia && hora >= h.HoraInicio && hora < h.HoraFin);
    }
}

public static class ReglasUsuario
{
    // Mínimo 8 caracteres, al menos una letra y un número.
    public static bool PasswordValida(string? p) =>
        !string.IsNullOrEmpty(p) && p.Length >= 8 && Regex.IsMatch(p, "[A-Za-z]") && Regex.IsMatch(p, "[0-9]");
}

public static class ReglasSolicitud
{
    public const int MaxPendientesPorCiudadano = 5;
    public const double MinKg = 0.5, MaxKg = 1000;

    public static void ValidarNueva(double cantidadKg, int pendientesActuales)
    {
        if (cantidadKg < MinKg || cantidadKg > MaxKg)
            throw new ReglaNegocioException($"La cantidad debe estar entre {MinKg} y {MaxKg} kg.");
        if (pendientesActuales >= MaxPendientesPorCiudadano)
            throw new ReglaNegocioException($"Solo puedes tener {MaxPendientesPorCiudadano} solicitudes pendientes a la vez.");
    }

    // Pendiente -> Aceptada: solo un recolector activo y verificado.
    public static void Aceptar(SolicitudRecojo s, Usuario recolector, DateTime ahoraUtc)
    {
        if (recolector.Rol != Roles.Recolector || !recolector.Activo || !recolector.Verificado)
            throw new ReglaNegocioException("Solo un recolector activo y verificado puede aceptar recojos.");
        if (s.Estado == EstadoSolicitud.Aceptada || s.Estado == EstadoSolicitud.Recogida)
            throw new ReglaNegocioException("La solicitud ya fue aceptada por otro recolector.", conflicto: true);
        if (s.Estado != EstadoSolicitud.Pendiente)
            throw new ReglaNegocioException("Solo se pueden aceptar solicitudes pendientes.");
        s.Estado = EstadoSolicitud.Aceptada;
        s.RecolectorId = recolector.Id;
        s.FechaAceptacion = ahoraUtc;
    }

    // Aceptada -> Recogida: solo el recolector asignado, con los kg reales.
    public static void Recoger(SolicitudRecojo s, int recolectorId, double kgReales, DateTime ahoraUtc)
    {
        if (s.Estado != EstadoSolicitud.Aceptada)
            throw new ReglaNegocioException("Solo se pueden recoger solicitudes aceptadas.");
        if (s.RecolectorId != recolectorId)
            throw new ReglaNegocioException("Solo el recolector asignado puede marcar el recojo.");
        if (kgReales <= 0 || kgReales > MaxKg)
            throw new ReglaNegocioException($"Los kg reales deben ser mayores que 0 y como máximo {MaxKg}.");
        s.Estado = EstadoSolicitud.Recogida;
        s.KgReales = kgReales;
        s.FechaRecojo = ahoraUtc;
    }

    // Pendiente -> Cancelada: solo el ciudadano dueño.
    public static void Cancelar(SolicitudRecojo s, int ciudadanoId)
    {
        if (s.CiudadanoId != ciudadanoId)
            throw new ReglaNegocioException("Solo el dueño puede cancelar la solicitud.");
        if (s.Estado != EstadoSolicitud.Pendiente)
            throw new ReglaNegocioException("Solo se pueden cancelar solicitudes pendientes.");
        s.Estado = EstadoSolicitud.Cancelada;
    }

    // Calificar: dueño, solicitud recogida, una sola vez, 1 a 5.
    public static void Calificar(SolicitudRecojo s, int ciudadanoId, int calificacion, string? comentario)
    {
        if (s.CiudadanoId != ciudadanoId)
            throw new ReglaNegocioException("Solo el dueño puede calificar.");
        if (s.Estado != EstadoSolicitud.Recogida)
            throw new ReglaNegocioException("Solo se califica un recojo ya realizado.");
        if (s.Calificacion.HasValue)
            throw new ReglaNegocioException("Esta solicitud ya fue calificada.", conflicto: true);
        if (calificacion < 1 || calificacion > 5)
            throw new ReglaNegocioException("La calificación debe ser de 1 a 5.");
        s.Calificacion = calificacion;
        s.Comentario = comentario?.Trim();
    }
}
