using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Tests;

// Pruebas unitarias de las reglas de negocio (no usan base de datos ni HTTP).
public class GeoTests
{
    [Fact]
    public void Distancia_mismo_punto_es_cero() =>
        Assert.Equal(0, Geo.DistanciaKm(-12.12, -77.03, -12.12, -77.03), 6);

    [Fact]
    public void Distancia_Miraflores_a_Lima_Centro_es_aprox_8_km()
    {
        double km = Geo.DistanciaKm(-12.1211, -77.0297, -12.0464, -77.0428);
        Assert.InRange(km, 8.0, 9.0);
    }

    [Fact]
    public void Distancia_es_simetrica() =>
        Assert.Equal(Geo.DistanciaKm(-12.0, -77.0, -11.9, -77.1), Geo.DistanciaKm(-11.9, -77.1, -12.0, -77.0), 9);
}

public class HorarioTests
{
    // Lunes de 08:00 a 18:00.
    private static readonly List<Horario> LunesOchoASeis =
        [new Horario { DiaSemana = 1, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(18, 0) }];

    // 2026-11-23 es lunes.
    [Theory]
    [InlineData(8, 0, true)]     // borde de inicio: abierto
    [InlineData(12, 30, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 0, false)]   // borde de fin: cerrado
    [InlineData(7, 59, false)]
    public void Abierto_segun_hora(int h, int m, bool esperado) =>
        Assert.Equal(esperado, HorarioLima.EstaAbierto(LunesOchoASeis, new DateTime(2026, 11, 23, h, m, 0)));

    [Fact]
    public void Cerrado_otro_dia() =>
        Assert.False(HorarioLima.EstaAbierto(LunesOchoASeis, new DateTime(2026, 11, 24, 10, 0, 0))); // martes

    [Fact]
    public void Hora_Lima_es_UTC_menos_5() =>
        Assert.Equal(new DateTime(2026, 11, 23, 7, 0, 0), HorarioLima.Ahora(new DateTime(2026, 11, 23, 12, 0, 0)));
}

public class PasswordTests
{
    [Theory]
    [InlineData("Demo2026!", true)]
    [InlineData("abcdefg1", true)]
    [InlineData("corta1", false)]        // menos de 8
    [InlineData("soloLetrasAqui", false)]
    [InlineData("12345678", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valida_password(string? p, bool esperado) => Assert.Equal(esperado, ReglasUsuario.PasswordValida(p));
}

public class SolicitudTests
{
    private static readonly DateTime Ahora = new(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc);
    private static Usuario Recolector(int id = 10, bool verificado = true, bool activo = true) =>
        new() { Id = id, Rol = Roles.Recolector, Verificado = verificado, Activo = activo };
    private static SolicitudRecojo Nueva() => new() { Id = 1, CiudadanoId = 5, CantidadEstimadaKg = 10 };

    [Theory]
    [InlineData(0.4)]
    [InlineData(1000.1)]
    public void Cantidad_fuera_de_rango_falla(double kg) =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.ValidarNueva(kg, 0));

    [Fact]
    public void Maximo_cinco_pendientes()
    {
        ReglasSolicitud.ValidarNueva(5, 4); // la quinta sí
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.ValidarNueva(5, 5));
    }

    [Fact]
    public void Aceptar_pendiente_la_asigna()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(), Ahora);
        Assert.Equal(EstadoSolicitud.Aceptada, s.Estado);
        Assert.Equal(10, s.RecolectorId);
        Assert.Equal(Ahora, s.FechaAceptacion);
    }

    [Fact]
    public void Recolector_no_verificado_no_puede_aceptar() =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Aceptar(Nueva(), Recolector(verificado: false), Ahora));

    [Fact]
    public void Recolector_inactivo_no_puede_aceptar() =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Aceptar(Nueva(), Recolector(activo: false), Ahora));

    [Fact]
    public void Doble_aceptacion_es_conflicto()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        var ex = Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Aceptar(s, Recolector(11), Ahora));
        Assert.True(ex.Conflicto);
    }

    [Fact]
    public void No_se_acepta_una_cancelada()
    {
        var s = Nueva();
        ReglasSolicitud.Cancelar(s, 5);
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Aceptar(s, Recolector(), Ahora));
    }

    [Fact]
    public void Recoger_aceptada_registra_kg()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        ReglasSolicitud.Recoger(s, 10, 12.5, Ahora);
        Assert.Equal(EstadoSolicitud.Recogida, s.Estado);
        Assert.Equal(12.5, s.KgReales);
    }

    [Fact]
    public void Recoger_pendiente_falla() =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Recoger(Nueva(), 10, 5, Ahora));

    [Fact]
    public void Solo_el_recolector_asignado_recoge()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Recoger(s, 99, 5, Ahora));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Kg_reales_invalidos(double kg)
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Recoger(s, 10, kg, Ahora));
    }

    [Fact]
    public void Cancelar_solo_el_duenio() =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Cancelar(Nueva(), 999));

    [Fact]
    public void No_se_cancela_una_aceptada()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(), Ahora);
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Cancelar(s, 5));
    }

    [Fact]
    public void Calificar_recogida_una_sola_vez()
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        ReglasSolicitud.Recoger(s, 10, 3, Ahora);
        ReglasSolicitud.Calificar(s, 5, 4, " Bien ");
        Assert.Equal(4, s.Calificacion);
        Assert.Equal("Bien", s.Comentario);
        Assert.True(Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Calificar(s, 5, 5, null)).Conflicto);
    }

    [Fact]
    public void No_se_califica_si_no_fue_recogida() =>
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Calificar(Nueva(), 5, 5, null));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Calificacion_fuera_de_rango(int c)
    {
        var s = Nueva();
        ReglasSolicitud.Aceptar(s, Recolector(10), Ahora);
        ReglasSolicitud.Recoger(s, 10, 3, Ahora);
        Assert.Throws<ReglaNegocioException>(() => ReglasSolicitud.Calificar(s, 5, c, null));
    }
}
